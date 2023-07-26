using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SyncSFTP
{
    public struct Config
    {
        public string address { get; set; }
        public int port { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string localDir { get; set; }
        public string remoteDir { get; set; }
        public int syncInterval { get; set; }

        public int maxBackupSizeGBs { get; set; }
    }
}
