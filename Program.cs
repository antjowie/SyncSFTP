/**
 * A tool to downlaod a SFTP directory locally
 * It will download any new files and not remove anything locally
 * Additionally, it doesn't respect new write times and will only download new files
 */
using Renci.SshNet;
using SyncSFTP;
using Konsole;
using Renci.SshNet.Sftp;
using System.Text.Json;

var window = Window.Open();
window.CursorVisible = false;
var statusLog = window.SplitLeft("status");
var transferLog = window.SplitRight("transfers");

var nextSyncDatum = DateTime.Now;
var bSyncIsOngoing = false;
var localDir = "";
// Find and parse config
Config config = new Config();
try
{
    config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"));
}
catch (Exception e)
{
    statusLog.Write("Failed to read config file, generating one...");
    using (var file = File.CreateText("config.json"))
    {
        file.Write(JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
    statusLog.Write("Done, press enter to close");
    Console.ReadKey();
    return;
}
localDir = Path.Combine(Environment.CurrentDirectory, config.localDir);
Directory.CreateDirectory(localDir);

// Create the connection
var connectionInfo = new ConnectionInfo(
    config.address,
    config.port,
    config.username,
    new PasswordAuthenticationMethod(config.username, config.password),
    new PrivateKeyAuthenticationMethod("rsa.key"));
using (var client = new SftpClient(connectionInfo))
{
    statusLog.WriteLine($"Attempting connection to: {config.address}...");
    client.Connect();
    PrintStatusInfo();

    var syncFiles = () =>
    {
        bSyncIsOngoing = true;
        transferLog.Clear();
        statusLog.Clear();
        PrintStatusInfo();
        var localFiles = GetLocalFilePaths().Select(file => Path.GetFileName(file));

        var remoteFiles = client.ListDirectory(config.remoteDir);
        var remoteFileNames = remoteFiles.Select(file => file.FullName);
        //transferLog.WriteLine("local files {0}", localFiles);
        //transferLog.WriteLine("remote files {0}", remoteFileNames);

        const int bufferSize = 1024 * 1024; // 1MB buffer sounds fine
        var downloadFile = (SftpFile remoteFile) =>
        {
            var fileLength = (int)remoteFile.Length;
            using (var localFile = File.Create(Path.Combine(localDir, remoteFile.Name), bufferSize, FileOptions.SequentialScan))
            {
                var bar = new ProgressBar(transferLog, fileLength);
                bar.Refresh(0, remoteFile.Name);
                client.DownloadFile(remoteFile.FullName, localFile, (bytesRead) =>
                {
                    var elapsedTime = (DateTime.Now - nextSyncDatum).Seconds + 1;
                    bar.Refresh((int)bytesRead, $"{(float)bytesRead / elapsedTime / 1024 / 1024:0.00}Mb/s {remoteFile.Name}");
                });
            }
        };

        var filesToSync = remoteFiles.Where(file => !localFiles.Contains(Path.GetFileName(file.Name)));
        if (filesToSync.Any())
        {
            Task.WhenAll(filesToSync.Select(file => Task.Run(() => downloadFile(file)))).Wait();
        }
        else
        {
            transferLog.WriteLine("Found no new files");
        }
        bSyncIsOngoing = false;
        nextSyncDatum = DateTime.Now + TimeSpan.FromSeconds(config.syncInterval);
    };

    while (true)
    {
        if (DateTime.Now >= nextSyncDatum)
        {
            syncFiles();
        }
        PrintStatusInfo();
        Thread.Sleep(1000);
    }
}
void PrintStatusInfo()
{
    statusLog.CursorLeft = statusLog.CursorTop = 0;
    statusLog.WriteLine($"Connected to: {config.address}");
    statusLog.WriteLine($"Files are stored at {localDir}");
    statusLog.WriteLine($"Currently storing {GetLocalFilePaths().Length} files");
    statusLog.WriteLine($"Sync status: {(bSyncIsOngoing ? "Ongoing!" : "Waiting...")}");
    if (!bSyncIsOngoing)
    {
        statusLog.WriteLine($"Next sync will occur at {nextSyncDatum} in {(nextSyncDatum - DateTime.Now).Seconds} seconds");
    }
    statusLog.WriteLine($"Use CTRL+C to close application");
}

string[] GetLocalFilePaths()
{
    return Directory.GetFiles(config.localDir);
}
