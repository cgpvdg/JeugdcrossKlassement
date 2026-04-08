using System.IO;
using System.Text.Json;
using jeugdcrossdata.Models;

namespace jeugdcrossdata.Services;

public sealed class UploadSettingsStore
{
    private readonly string _settingsPath;

    public UploadSettingsStore()
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "upload-settings.json");
    }

    public async Task<UploadSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new UploadSettings();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<UploadSettings>(stream);
        return settings ?? new UploadSettings();
    }

    public string GetSettingsPath() => _settingsPath;
}
