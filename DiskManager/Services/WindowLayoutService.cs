using System;
using System.IO;
using System.Text.Json;

namespace DiskManager.Services;

public sealed class WindowLayout
{
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 760;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Maximized { get; set; }
}

public sealed class WindowLayoutService
{
    private readonly string? _filePath;

    public WindowLayoutService()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DiskManager");

            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "window.json");
        }
        catch (Exception)
        {
            _filePath = null;
        }
    }

    public WindowLayout Load()
    {
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            return new WindowLayout();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var layout = JsonSerializer.Deserialize<WindowLayout>(json);
            if (layout == null || layout.Width < 900 || layout.Height < 600)
            {
                return new WindowLayout();
            }

            return layout;
        }
        catch (Exception)
        {
            return new WindowLayout();
        }
    }

    public void Save(WindowLayout layout)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, true);
        }
        catch (Exception)
        {
        }
    }
}