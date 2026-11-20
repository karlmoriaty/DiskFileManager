namespace DiskManager.Utils;

public static class SizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < Units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", size, Units[order]);
    }
}