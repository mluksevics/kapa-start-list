using System.Text.Json;

namespace StartRef.Desktop;

public class AppSettings
{
    public string ApiBaseUrl { get; set; } = "https://startref.azurewebsites.net/";
    public string ApiKey { get; set; } = "marcisTestKey";
    public string DbIsamPath { get; set; } = "";
    public int SyncIntervalSeconds { get; set; } = 60;
    public bool AutoSyncEnabled { get; set; } = true;
    public bool FailureSoundEnabled { get; set; } = true;
    public string DeviceName { get; set; } = "desktop";
    public int DayNo { get; set; } = 1;
    public int DbCodePage { get; set; } = 1257;
    public DateTimeOffset LastServerTimeUtc { get; set; } = DateTimeOffset.MinValue;

    private static readonly string SettingsPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        AppSettings settings;
        if (!File.Exists(SettingsPath))
        {
            settings = new AppSettings();
        }
        else
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                settings = new AppSettings();
            }
        }

        // Apply defaults for fields that were empty before defaults existed
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            settings.ApiBaseUrl = new AppSettings().ApiBaseUrl;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            settings.ApiKey = new AppSettings().ApiKey;

        return settings;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
