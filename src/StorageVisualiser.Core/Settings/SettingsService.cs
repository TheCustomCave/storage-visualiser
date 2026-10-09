using System;
using System.IO;
using System.Text.Json;

namespace StorageVisualiser.Core.Settings;

public sealed class SettingsService
{
    public const string SettingsFileName = "settings.json";
    private readonly string? _configuredFilePath;

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService(string? customFilePath = null)
    {
        _configuredFilePath = customFilePath;
        Current = Load();
    }

    public string GetActiveFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_configuredFilePath))
        {
            return _configuredFilePath;
        }

        // Portable mode: try next to the executable first
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localSettingsPath = Path.Combine(appDir, SettingsFileName);

        if (File.Exists(localSettingsPath))
        {
            return localSettingsPath;
        }

        // Check if app directory is writable
        if (IsDirectoryWritable(appDir))
        {
            return localSettingsPath;
        }

        // Fall back to %LOCALAPPDATA%\StorageVisualiser\settings.json
        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StorageVisualiser");
        return Path.Combine(localAppData, SettingsFileName);
    }

    public AppSettings Load()
    {
        var filePath = GetActiveFilePath();
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
                if (loaded != null)
                {
                    Current = loaded;
                    return loaded;
                }
            }
            catch
            {
                // Fall back to defaults on corrupt or unreadable settings file
            }
        }

        Current = new AppSettings();
        return Current;
    }

    public bool Save(AppSettings settings)
    {
        Current = settings;
        var targetPath = GetActiveFilePath();

        try
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            File.WriteAllText(targetPath, json);
            SettingsChanged?.Invoke(settings);
            return true;
        }
        catch
        {
            // If primary location failed (e.g., access denied on portable path), try LocalAppData fallback
            try
            {
                var fallbackDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StorageVisualiser");
                Directory.CreateDirectory(fallbackDir);
                var fallbackPath = Path.Combine(fallbackDir, SettingsFileName);

                var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
                File.WriteAllText(fallbackPath, json);
                SettingsChanged?.Invoke(settings);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool IsDirectoryWritable(string dirPath)
    {
        try
        {
            var testFile = Path.Combine(dirPath, $".writable_test_{Guid.NewGuid():N}.tmp");
            using (var fs = File.Create(testFile, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
