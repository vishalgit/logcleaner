using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;

namespace logcleaner
{
    public class Application
    {
        public Application(IConfiguration config, ILogger logger)
        {
            Config = config;
            Logger = logger;
        }
        public IConfiguration Config { get; private set; }
        public ILogger Logger { get; private set; }
        public void Run()
        {
            var logDirs = Config.GetSection("Application:PathToLogDirectory").GetChildren().Select(x=>x.Value);
            var searchPatterns = Config.GetSection("Application").GetValue<string>("FileTypesToArchive")
            .Split(",".ToCharArray(),StringSplitOptions.RemoveEmptyEntries);
            var fileRetentionDays = Config.GetSection("Application").GetValue<int>("FileRetentionDays");
            var drvs = new List<string>();
            foreach (var logDir in logDirs)
            {
                Logger.Information($"The path to log directory: {logDir}");
                var rootDrive = Path.GetPathRoot(logDir);
                if(!drvs.Contains(rootDrive))
                {
                    drvs.Add(rootDrive);
                    Logger.Information($"The drive is : {rootDrive}");
                    var drive = DriveInfo.GetDrives().Where(d=>d.Name == rootDrive).FirstOrDefault();
                    var freeSpacePercent = ((decimal)drive.TotalFreeSpace/drive.TotalSize)*100;
                    Logger.Information($"Total free space on drive: {freeSpacePercent.ToString("#.##")}%");
                }
                var filesToArchive = new List<string>();
                foreach (var searchPattern in searchPatterns)
                {
                    foreach (var file in Directory.EnumerateFiles(logDir,searchPattern,SearchOption.AllDirectories))
                    {

                        var fileInfo = new FileInfo(file);
                        if(fileInfo.LastWriteTime.Date <= DateTime.Today.AddDays(-1*fileRetentionDays)){
                            filesToArchive.Add(file);
                        }
                    }    
                }        
                // Create Zip and delete files
                var zipFileName = Path.Combine(logDir,$"logArchive{DateTime.Now.ToString("yyyyMMdd")}.zip");
                using(var zipStream = File.Open(zipFileName,FileMode.OpenOrCreate))
                {
                    using(var archive = new ZipArchive(zipStream,ZipArchiveMode.Update))
                    {
                        foreach (var file in filesToArchive)
                        {
                            var fileEntry = archive.CreateEntry(Path.GetFileName(file),CompressionLevel.Optimal);
                            using(var fileWriter = new StreamWriter(fileEntry.Open()))
                            {
                                fileWriter.Write(File.ReadAllText(file));
                            }
                            try
                            {
                                File.Delete(file);
                                Logger.Information($"File archived with name: {file}");
                            }
                            catch (Exception ex)
                            {
                                    Logger.Error(ex.Message);
                            }
                        }
                    }
                }
            }
            
            // delete the zipped files

            // find all the zip files older retention date
            // delete the zip files
            // Log current drive space
            // send mail if total space is less than 20% of drive space

        }
    }
}