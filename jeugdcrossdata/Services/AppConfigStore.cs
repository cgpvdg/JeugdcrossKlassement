using System.IO;
using System.Text;
using System.Text.Json;
using jeugdcrossdata.Models;

namespace jeugdcrossdata.Services;

public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public AppConfigStore()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "jeugdcrossdata");
        Directory.CreateDirectory(appData);
        _configPath = Path.Combine(appData, "config.json");
    }

    public async Task<AppConfig> LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            return new AppConfig();
        }

        await using var stream = File.OpenRead(_configPath);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream);
        return config ?? new AppConfig();
    }

    public async Task SaveAsync(AppConfig config)
    {
        await using var stream = File.Create(_configPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions);
    }
}

