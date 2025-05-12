namespace SyncSFTP
{
    public struct Config
    {
        public Config()
        {
        }

        public string address { get; set; }
        public int port { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string localDir { get; set; } = "backups/";
        public string remoteDir { get; set; } = "backups/";
        public int syncInterval { get; set; } = 60;

        public int maxBackupSizeGBs { get; set; } = 10;
    }
}
