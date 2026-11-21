using System;
using System.IO;
using System.Text.Json;
using DiskManager.Models;

namespace DiskManager.Services;

public sealed class SettingsService
{
    private readonly string? _filePath;

    public SettingsService(string? directory = null)
    {
        try
        {
            var baseDirectory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DiskManager");

            Directory.CreateDirectory(baseDirectory);
            _filePath = Path.Combine(baseDirectory, "settings.json");
        }
        catch (Exception)
        {
            _filePath = null;
        }
    }

    public AppSettings Load()
    {
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, true);
        }
        catch (Exception)
        {
        }
    }
}