namespace StartRef.Desktop;

/// <summary>
/// Sync service between the StartRef API and DBISAM (via DbBridge DLL).
///
/// Each cycle:
///   1. PULL: GET /runners from API → get latest state from field devices
///   2. WRITE to DBISAM: write ChipNr changes from field devices back to DBISAM
///   3. SKIP (not yet supported by DLL):
///        - DNS write-back (needs DbChangeDnsByStartNr)
///        - Bulk read/push (needs DbReadAllTeiln)
///
/// DLL gaps to request from the Delphi developer:
///   • DbReadAllTeiln(ctx, buffer, bufferSize) — enumerate all participants
///   • DbChangeDnsByStartNr(ctx, startNr, dnsFlag) — set DNS/AufgabeTyp
/// </summary>
public class SyncService
{
    private readonly ApiClient _api;
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<string> _log;

    private System.Windows.Forms.Timer? _timer;

    public SyncService(
        ApiClient api,
        Func<AppSettings> getSettings,
        Action<string> log)
    {
        _api = api;
        _getSettings = getSettings;
        _log = log;
    }

    public void Start()
    {
        var settings = _getSettings();
        _timer = new System.Windows.Forms.Timer
        {
            Interval = settings.SyncIntervalSeconds * 1000
        };
        _timer.Tick += async (_, _) =>
        {
            if (_getSettings().AutoSyncEnabled)
                await RunCycleAsync(forcePush: false);
        };
        _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    public void UpdateInterval(int seconds)
    {
        if (_timer is null) return;
        _timer.Interval = seconds * 1000;
    }

    /// <summary>Runs one sync cycle: PULL from API → write ChipNr changes to DBISAM.</summary>
    public async Task RunCycleAsync(bool forcePush)
    {
        var settings = _getSettings();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

        // 1. PULL
        _log($"{Ts()} Pulling from API...");
        DateTimeOffset? changedSince = settings.LastServerTimeUtc == DateTimeOffset.MinValue
            ? null
            : settings.LastServerTimeUtc;

        var pullResult = await _api.GetRunnersAsync(today, changedSince);
        if (pullResult is null)
        {
            _log($"{Ts()} PULL failed — API unreachable.");
            return;
        }

        // Log field-device status changes
        foreach (var r in pullResult.Runners)
        {
            if (r.StatusId != 1)
                _log($"{Ts()} PULL #{r.StartNumber} {r.Name} {r.Surname} – Status={r.StatusName} (by: {r.LastModifiedBy})");
        }

        // 2. Write ChipNr changes to DBISAM (runners modified by field devices, not desktop)
        var chipUpdates = pullResult.Runners
            .Where(r => !string.IsNullOrEmpty(r.SiChipNo) &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chipUpdates.Count > 0 && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var db = new DbBridgeService(_log);
            if (db.Open(settings.DbIsamPath))
            {
                foreach (var r in chipUpdates)
                {
                    if (!int.TryParse(r.SiChipNo, out int chipNr)) continue;
                    var res = db.ChangeChipNrByStartNr(settings.DayNo, chipNr, r.StartNumber);
                    _log($"{Ts()} DBISAM ChipNr #{r.StartNumber} → {chipNr}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                }
            }
            else
            {
                _log($"{Ts()} DBISAM not available — ChipNr writes skipped. Check DB path and DbBridge.dll.");
            }
        }

        // 3. DNS write — not yet supported by DLL
        int dnsCount = pullResult.Runners.Count(r => r.StatusId == 3);
        if (dnsCount > 0)
            _log($"{Ts()} [WARN] {dnsCount} DNS runner(s) NOT written to DBISAM — DLL lacks DbChangeDnsByStartNr.");

        // 4. Bulk read/push — not yet supported by DLL
        if (forcePush)
            _log($"{Ts()} [WARN] Force Push skipped — DLL lacks DbReadAllTeiln (bulk read). Ask Delphi developer.");
        else if (pullResult.Runners.Count == 0)
            _log($"{Ts()} Sync: no changes from API.");
        else
            _log($"{Ts()} Sync: pulled {pullResult.Runners.Count} runner(s). [Bulk push skipped — DLL lacks DbReadAllTeiln]");

        // Update watermark
        settings.LastServerTimeUtc = pullResult.ServerTimeUtc;
        settings.Save();
        UpdateLastSync();
    }

    private void UpdateLastSync()
    {
        // Trigger UI update via log (MainForm listens to log callback which calls UpdateLastSyncLabel)
    }

    private static string Ts() => DateTime.Now.ToString("HH:mm:ss");
}
