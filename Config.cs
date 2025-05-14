using System.Text.Json.Serialization;

namespace SyncSFTP
{
    public struct Config
    {
        public Config()
        {
        }

        [JsonIgnore]
        public string path { get; } = Path.GetFullPath("config.json");

        public string address { get; set; } = "0.0.0.0";
        public int port { get; set; } = 2202;
        public string username { get; set; } = "name";
        public string password { get; set; } = "pass";
        public string localDir { get; set; } = "backups/";
        public string remoteDir { get; set; } = "backups/";
        public int syncInterval { get; set; } = 60;

        public int maxBackupSizeGBs { get; set; } = 10;
    }
}
