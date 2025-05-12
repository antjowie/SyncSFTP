using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncSFTP
{
    public struct PurgedFiles
    {
        public PurgedFiles()
        {
        }

        [JsonIgnore]
        public string path => Path.GetFullPath("purged_files.json");

        public void Write()
        {
            using (var file = File.CreateText(path))
            {
                file.Write(JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        public List<string> purgedFiles{ get; set; } = new List<string>();
    }
}
