## SyncSFTP [![Build](https://github.com/antjowie/syncsftp/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/antjowie/syncsftp/actions/workflows/dotnet-ci.yml)
Command line utility to periodically download the contents of a folder on an SFTP server. I use it to store the server backups locally. Made using .NET 7.0.x. 


> The application doesn't really sync, it downloads any new files it finds and purges the oldest ones if maxSize is exceeded

## Usage
1. Download the application from the [release page](https://github.com/antjowie/SyncSFTP/releases)
2. Run the .exe
   1. When booting for the first time, a config file is generated. Make sure to update the values
   2. To run the app on boot, add a shortcut to the executable in the startup folder (Windows key + R and then type `shell:startup`)

## Example of a config file
```json
{
  "address": "some.address.com",
  "port": 1234,
  "username": "<username>",
  "password": "<password>", 
  "localDir": "backups/",
  "remoteDir": "backups/",
  // How many seconds to wait before polling server
  "syncInterval": 60,
  // If > 0, on sync if total size exceeds value, purge oldest files
  "maxBackupSizeGBs": 10
}

```

![Showcase](Showcase.png)
