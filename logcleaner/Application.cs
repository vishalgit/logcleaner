using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections;
using System.Collections.Generic;
using MimeKit;
using MailKit.Net.Smtp;

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
            // Get Configuration
            var logDirs = Config.GetSection("ApplicationSettings:PathToLogDirectory").GetChildren().Select(x=>x.Value).ToList();
            var searchPatterns = Config.GetSection("ApplicationSettings").GetValue<string>("FileTypesToArchive").Split(",".ToCharArray(),
                                                                                    StringSplitOptions.RemoveEmptyEntries);
            var fileRetentionDays = Config.GetSection("ApplicationSettings").GetValue<int>("FileRetentionDays");
            var zipFileRetentionDays = Config.GetSection("ApplicationSettings").GetValue<int>("ZipFileRetentionDays");
            var freeSpaceThresholdPercentage = Config.GetSection("ApplicationSettings").GetValue<int>("FreeSpaceThresholdPercentage");
          

            //loop each path and clean the logs
            foreach (var logDir in logDirs)
            {           
                // Find files to archive
                var filesToArchive = FindFiles(searchPatterns, fileRetentionDays, logDir);
                // Find zip files to archive
                var zipFilesToArchive = FindFiles(new[] { "*.zip" }, zipFileRetentionDays, logDir);
                // Create Zip File
                var zipFileName = Path.Combine(logDir, $"logArchive{DateTime.Now.ToString("yyyyMMdd")}.zip");
                ZipFiles(filesToArchive, zipFileName);
                // Delete files
                DeleteFiles(filesToArchive);
                // Delete zip files
                DeleteFiles(zipFilesToArchive);
            }
            //Find unique drives to check
            var drives = new List<string>();
            logDirs.ForEach((dir)=>
            {
                drives.Add(Path.GetPathRoot(dir));
            });
            drives = drives.Distinct().ToList();
            DriveInfo.GetDrives().ToList().ForEach((drv)=>{
                if(drives.Contains(drv.Name)){
                    Logger.Information($"Drive name: {drv.Name}");
                    var freeSpacePercent = ((decimal)drv.TotalFreeSpace / drv.TotalSize) * 100;
                    Logger.Information($"Total free space on drive: {freeSpacePercent.ToString("#.##")}%");
                    if(freeSpacePercent <= freeSpaceThresholdPercentage){
                        SendMail(drv.Name,freeSpacePercent,freeSpaceThresholdPercentage);
                    }
                }
            });
        }

        private void SendMail(string name, decimal freeSpacePercent, int freeSpaceThresholdPercentage)
        {
            var mailFrom = Config.GetSection("MailSettings").GetValue<string>("From");
            var mailTo = Config.GetSection("MailSettings").GetValue<string>("To");
            var mailSubject = Config.GetSection("MailSettings").GetValue<string>("Subject");
            var mailServer = Config.GetSection("MailSettings").GetValue<string>("SmtpServer");
            var pathFormat = Config.GetSection("Serilog:WriteTo:Args").GetValue<string>("pathFormat");
            
            var message = $@"Free space percent on Drive: {name}
             On Machine {Environment.MachineName} is {freeSpacePercent.ToString("#.##")}% 
             Which is less than threshold {freeSpaceThresholdPercentage}% 
             After cleaning archive files. Please check";
            Logger.Information(message);
            var mailMessage = new MimeMessage();
            mailMessage.From.Add(new MailboxAddress(mailFrom));
            mailMessage.To.Add(new MailboxAddress(mailTo));
            mailMessage.Subject = mailSubject;
            var builder = new BodyBuilder();
            builder.TextBody = message;
            var logFileName = pathFormat.Replace("{Date}",DateTime.Today.ToString("yyyyMMdd"));
            builder.Attachments.Add(logFileName);
            mailMessage.Body = builder.ToMessageBody();
            using(var smtpClient = new SmtpClient())
            {
                smtpClient.Connect(mailServer);
                smtpClient.Send(mailMessage);
                smtpClient.Disconnect(true);
            }
        }

        private void DeleteFiles(List<string>filesToDelete)
        {
           foreach (var file in filesToDelete)
           {
               try
               {
                   File.Delete(file);
                   Logger.Information($"Deleted File with name: {file}");
               }
               catch (Exception ex)
               {
                   Logger.Error($"Unable to delete file with name: {file}, due to exception: {ex.Message}");
               }
           }
        }

        private void ZipFiles(List<string> filesToArchive, string zipFileName)
        {
            using (var zipStream = File.Open(zipFileName, FileMode.OpenOrCreate))
            {
                Logger.Information($"Created/Opened zip file name: {zipFileName}");

                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Update))
                {
                    foreach (var file in filesToArchive)
                    {
                        var fileEntry = archive.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal);
                        using (var fileWriter = new StreamWriter(fileEntry.Open()))
                        {
                            fileWriter.Write(File.ReadAllText(file));
                            Logger.Information($"Zipped file with name: {file}");
                        }
                    }
                }
            }
        }

        private List<string>  FindFiles(string[] searchPatterns, int fileRetentionDays, string pathToDir)
        {
            var filesToArchive = new List<string>();
            foreach (var searchPattern in searchPatterns)
            {
                foreach (var file in Directory.EnumerateFiles(pathToDir, searchPattern, SearchOption.AllDirectories))
                {

                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime.Date <= DateTime.Today.AddDays(-1 * fileRetentionDays))
                    {
                        filesToArchive.Add(file);
                    }
                }
            }
            return filesToArchive;
        }
    }
}