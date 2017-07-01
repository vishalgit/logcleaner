using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StructureMap;

namespace logcleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            // Setting up configuration and property injection
            var configuration = new ConfigurationBuilder()
            .AddJsonFile("settings.json")
            .Build();
            // Setting up service provider for extension methods and structure map.
            var services = new ServiceCollection();
            //Set up logging
            var log = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();
            var container = new Container();
            container.Configure(config=>{
                config.Scan(_=>{
                    _.TheCallingAssembly();
                    _.WithDefaultConventions();
                });
                config.Populate(services);
                config.For<IConfiguration>().Use(()=>configuration);
                config.For<ILogger>().Use(()=>log);
            });
            var app = container.GetInstance<Application>();
            app.Run();            
        }
    }
}
