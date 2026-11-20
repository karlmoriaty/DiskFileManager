using System;
using System.Runtime.InteropServices;

namespace DiskManager.Services;

public sealed class RecycleBinService : IRecycleBinService
{
    private const uint SherbNoConfirmation = 0x00000001;
    private const uint SherbNoProgressUi = 0x00000002;
    private const uint SherbNoSound = 0x00000004;

    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOperation
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        public int AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShQueryRbInfo
    {
        public int CbSize;
        public long Size;
        public long NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOperation operation);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref ShQueryRbInfo info);

    public bool Recycle(string path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var exists = isDirectory ? System.IO.Directory.Exists(path) : System.IO.File.Exists(path);
        if (!exists)
        {
            return false;
        }

        var operation = new ShFileOperation
        {
            Func = FoDelete,
            From = path + "\0",
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi
        };

        var result = SHFileOperation(ref operation);
        return result == 0 && operation.AnyOperationsAborted == 0;
    }

    public bool Empty()
    {
        var result = SHEmptyRecycleBin(IntPtr.Zero, null, SherbNoConfirmation | SherbNoProgressUi | SherbNoSound);
        return result == 0;
    }

    public RecycleBinInfo Query()
    {
        var info = new ShQueryRbInfo { CbSize = Marshal.SizeOf<ShQueryRbInfo>() };
        var result = SHQueryRecycleBin(null, ref info);
        if (result != 0)
        {
            return new RecycleBinInfo(0, 0);
        }

        return new RecycleBinInfo(info.Size, (int)Math.Min(info.NumItems, int.MaxValue));
    }
}