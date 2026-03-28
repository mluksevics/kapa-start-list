namespace StartRef.Desktop;

/// <summary>
/// Bidirectional sync service between DBISAM and the StartRef API.
///
/// Each cycle:
///   1. PULL: GET /runners from API → get latest state (statuses from field devices)
///   2. WRITE DNS to DBISAM: runners with statusId=3 from API → mark DNS in DBISAM
///   3. READ from DBISAM: get full start list (includes OE12 edits + DNS just written)
///   4. PUSH to API: PUT /runners bulk upload; API diffs and applies status rules
/// </summary>
public class SyncService
{
    private readonly ApiClient _api;
    private readonly DbIsamReader _dbReader;
    private readonly DbIsamWriter _dbWriter;
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<string> _log;

    private System.Windows.Forms.Timer? _timer;

    public SyncService(
        ApiClient api,
        DbIsamReader dbReader,
        DbIsamWriter dbWriter,
        Func<AppSettings> getSettings,
        Action<string> log)
    {
        _api = api;
        _dbReader = dbReader;
        _dbWriter = dbWriter;
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

    /// <summary>Runs one full sync cycle (PULL → WRITE DNS → READ → PUSH).</summary>
    public async Task RunCycleAsync(bool forcePush)
    {
        var settings = _getSettings();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

        // 1. PULL
        _log($"{Ts()} Auto-sync: pulling from API...");
        DateTimeOffset? changedSince = settings.LastServerTimeUtc == DateTimeOffset.MinValue
            ? null
            : settings.LastServerTimeUtc;

        var pullResult = await _api.GetRunnersAsync(today, changedSince);
        if (pullResult is null)
        {
            _log($"{Ts()} PULL failed — API unreachable.");
            return;
        }

        // Log field-device changes
        foreach (var r in pullResult.Runners)
        {
            if (r.StatusId == 3 || r.StatusId == 2)
            {
                _log($"{Ts()} PULL #{r.StartNumber} {r.Name} {r.Surname} – Status changed to \"{r.StatusName}\" (by: {r.LastModifiedBy})");
            }
        }

        // 2. WRITE DNS to DBISAM
        var dnsFromApi = pullResult.Runners
            .Where(r => r.StatusId == 3)
            .Select(r => r.StartNumber)
            .ToList();

        if (dnsFromApi.Count > 0)
        {
            try
            {
                _dbWriter.WriteDnsStatuses(dnsFromApi);
                foreach (var sn in dnsFromApi)
                {
                    var r = pullResult.Runners.First(x => x.StartNumber == sn);
                    _log($"{Ts()} DBISAM WRITE DNS: #{sn} {r.Name} {r.Surname}");
                }
            }
            catch (Exception ex)
            {
                _log($"{Ts()} DBISAM WRITE failed: {ex.Message}");
            }
        }

        // 3. READ from DBISAM
        List<BulkRunnerDto> dbRunners;
        try
        {
            dbRunners = _dbReader.ReadAll();
        }
        catch (Exception ex)
        {
            _log($"{Ts()} DBISAM READ failed: {ex.Message}");
            return;
        }

        // 4. PUSH to API
        var lastModifiedUtc = forcePush ? DateTimeOffset.UtcNow : settings.LastServerTimeUtc;
        var request = new BulkUploadRequest
        {
            Source = settings.DeviceName,
            LastModifiedUtc = lastModifiedUtc,
            Runners = dbRunners
        };

        var pushResult = await _api.BulkUploadAsync(today, request);
        if (pushResult is null)
        {
            _log($"{Ts()} PUSH failed — API unreachable.");
            return;
        }

        // Update watermark
        settings.LastServerTimeUtc = pullResult.ServerTimeUtc;
        settings.Save();

        if (pushResult.Inserted + pushResult.Updated == 0)
        {
            _log($"{Ts()} Auto-sync: no changes (unchanged={pushResult.Unchanged})");
        }
        else
        {
            _log($"{Ts()} PUSH: inserted={pushResult.Inserted} updated={pushResult.Updated} unchanged={pushResult.Unchanged} skipped={pushResult.SkippedAsOlder}");
        }
    }

    private static string Ts() => DateTime.Now.ToString("HH:mm:ss");
}
