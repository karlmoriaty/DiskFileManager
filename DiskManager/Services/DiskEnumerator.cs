using System;
using System.Collections.Generic;
using System.IO;
using DiskManager.Models;

namespace DiskManager.Services;

public static class DiskEnumerator
{
    public static List<DiskInfo> GetReadyDisks()
    {
        var disks = new List<DiskInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }

                var total = drive.TotalSize;
                var free = drive.AvailableFreeSpace;
                disks.Add(new DiskInfo
                {
                    DriveLetter = drive.Name,
                    Name = GetVolumeLabel(drive),
                    FileSystem = GetFileSystem(drive),
                    TotalSize = total,
                    UsedSpace = total - free,
                    FreeSpace = free
                });
            }
            catch (Exception)
            {
            }
        }

        return disks;
    }

    private static string GetVolumeLabel(DriveInfo drive)
    {
        try
        {
            return drive.VolumeLabel;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string GetFileSystem(DriveInfo drive)
    {
        try
        {
            return drive.DriveFormat;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}