using Microsoft.Extensions.Configuration;
namespace logcleaner
{
    public class Config
    {
        public string FilesLocation { get; set; }
        public string FilesPattern { get; set; }
        public string DaysToKeepFiles { get; set; }
    }
}