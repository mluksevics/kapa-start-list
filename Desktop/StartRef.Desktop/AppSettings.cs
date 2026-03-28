using System.Text.Json;

namespace StartRef.Desktop;

public class AppSettings
{
    public string ApiBaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string DbIsamPath { get; set; } = "";
    public int SyncIntervalSeconds { get; set; } = 60;
    public bool AutoSyncEnabled { get; set; } = true;
    public string DeviceName { get; set; } = "desktop";
    public DateTimeOffset LastServerTimeUtc { get; set; } = DateTimeOffset.MinValue;

    private static readonly string SettingsPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
