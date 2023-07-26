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

var bytesToKbs = 1f / 1024;
var bytesToMbs = bytesToKbs / 1024;
var bytesToGbs = bytesToMbs / 1024;

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
    statusLog.Write($"Caught exception {e}");
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
                var bar = new ProgressBar(transferLog, PbStyle.DoubleLine, fileLength);
                bar.Refresh(0, remoteFile.Name);
                client.DownloadFile(remoteFile.FullName, localFile, (bytesRead) =>
                {
                    var elapsedTime = (DateTime.Now - nextSyncDatum).Seconds + 1;
                    var bytesPerSecond = (float)bytesRead / elapsedTime;
                    var secondsLeft = (int)(fileLength / (bytesPerSecond == 0 ? 1 : bytesPerSecond));
                    bar.Refresh((int)bytesRead, $"{bytesPerSecond * bytesToMbs:0.00}Mb/s {secondsLeft}s left {remoteFile.Name}");
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

        // Handle purging files
        if (config.maxBackupSizeGBs > 0)
        {
            var maxBytes = config.maxBackupSizeGBs / bytesToGbs;
            var currentSize = 0;
            new DirectoryInfo(config.localDir).GetFiles()
            .OrderBy(file => file.CreationTime).ToList().ForEach(file =>
            {
                // TODO hack out backups.json. If it's ever used for other projects make sure to add pattern matching in config
                if (file.Name.Contains("backups.json"))
                {
                    return;
                }

                currentSize += (int)file.Length;
                if (currentSize > maxBytes)
                {
                    transferLog.WriteLine($"Removing {file.Name}");
                    file.Delete();
                }
            });
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
    var directoryInfo = new DirectoryInfo(config.localDir);
    var backupSize = directoryInfo.GetFiles().Aggregate(0L, (current, info) => current + info.Length);
    var maxBackupSizeFeedback = config.maxBackupSizeGBs == 0 ? "" : $"/{config.maxBackupSizeGBs}";
    statusLog.CursorLeft = statusLog.CursorTop = 0;
    statusLog.WriteLine($"Connected to: {config.address}");
    statusLog.WriteLine($"Files are stored at {localDir}");
    statusLog.WriteLine($"Currently storing {GetLocalFilePaths().Length} files ({backupSize * bytesToGbs:00}{maxBackupSizeFeedback:00}GBs)");
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
