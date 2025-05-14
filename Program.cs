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

// Find and parse config
Config config = new Config();
try
{
    config = JsonSerializer.Deserialize<Config>(File.ReadAllText(config.path));
}
catch (Exception e)
{
    if (File.Exists(config.path)) {
        // It did exist but we couldn't parse it
        Console.WriteLine($"Caught exception {e}");
    }

    Console.WriteLine($"\nFailed to read config file (searched for \"{config.path}\"), generating one...");
    using (var file = File.CreateText(config.path))
    {
        file.Write(JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
    Console.WriteLine("Done, please make sure to fill out the config file. Press enter to close");
    Console.ReadKey();

    return;
}

PurgedFiles purgedFiles = new PurgedFiles();
try
{
    purgedFiles = JsonSerializer.Deserialize<PurgedFiles>(File.ReadAllText(purgedFiles.path));
}
catch (Exception)
{
    purgedFiles.Write();
}

//var width = Window.HostConsole.WindowWidth;
//var height = Window.HostConsole.WindowHeight;
//var window = Konsole.Platform.PlatformExtensions.LockConsoleResizing(new Window(), width, height);
//var console = window.Concurrent();
var console = Window.Open();
console.CursorVisible = false;
var statusLog = console.SplitLeft("status");
var transferLog = console.SplitRight("transfers");

var nextSyncDatum = DateTime.Now;
var bSyncIsOngoing = false;
var localDir = "";
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
            var fileBytes = (ulong)remoteFile.Length;
            using (var localFile = File.Create(Path.Combine(localDir, remoteFile.Name), bufferSize, FileOptions.SequentialScan))
            {
                var bar = new ProgressBar(transferLog, (int)(fileBytes * bytesToMbs), 40);
                bar.Refresh(0, $"{fileBytes * bytesToGbs:0.00}GB {remoteFile.Name}");
                client.DownloadFile(remoteFile.FullName, localFile, (bytesRead) =>
                {
                    var elapsedTime = (uint)(DateTime.Now - nextSyncDatum).Seconds + 1;
                    var bytesLeft = fileBytes - bytesRead;
                    var bytesPerSecond = bytesRead / elapsedTime;
                    var secondsLeft = bytesLeft / (bytesPerSecond == 0 ? 1 : bytesPerSecond);
                    bar.Refresh((int)(bytesRead * bytesToMbs), $"{fileBytes * bytesToGbs:0.00}GB {bytesPerSecond * bytesToMbs:0.00}Mb/s ({secondsLeft}s) {remoteFile.Name}");
                });
            }
        };

        var shouldDownloadFile = (SftpFile file) =>
        {
            var name = Path.GetFileName(file.Name);
            var shouldDownload = true;
            shouldDownload &= localFiles.Contains(name) == false;
            shouldDownload &= purgedFiles.purgedFiles.Contains(name) == false;
            // TODO hack out backups.json. If it's ever used for other projects make sure to add pattern matching in config
            shouldDownload &= name.Contains("backups.json") == false;
            return shouldDownload;
        };
        var filesToSync = remoteFiles.Where(shouldDownloadFile);
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
            var currentSizeGBs = 0f;
            new DirectoryInfo(config.localDir).GetFiles().OrderByDescending(file => file.CreationTimeUtc).ToList().ForEach(file =>
            {

                currentSizeGBs += file.Length * bytesToGbs;
                if (currentSizeGBs > config.maxBackupSizeGBs)
                {
                    transferLog.WriteLine($"Removing {file.Name}");
                    file.Delete();
                    purgedFiles.purgedFiles.Add(file.Name);
                }
            });

            purgedFiles.Write();
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
    statusLog.WriteLine($"Stored at: {localDir}");
    statusLog.WriteLine($"Stored {GetLocalFilePaths().Length} files ({backupSize * bytesToGbs:00}{maxBackupSizeFeedback:00}GB)");
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
