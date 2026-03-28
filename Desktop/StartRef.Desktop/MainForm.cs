namespace StartRef.Desktop;

public partial class MainForm : Form
{
    private AppSettings _settings;
    private readonly ApiClient _api;
    private readonly SyncService _syncService;
    private readonly string _logFilePath;

    private sealed record EtapItem(int DayNo, string Name, string Date)
    {
        public override string ToString() => $"Day {DayNo}: {Name}  ({Date})";
    }

    public MainForm()
    {
        InitializeComponent();
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        _settings = AppSettings.Load();
        _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_log.txt");

        _api = new ApiClient(() => _settings);
        _syncService = new SyncService(_api, () => _settings, AppendLog);

        LoadSettingsToUi();
        LoadStagesFromDb();

        if (_settings.AutoSyncEnabled)
            _syncService.Start();

        UpdateStatusLabel("Idle");
    }

    private void LoadSettingsToUi()
    {
        txtApiUrl.Text = _settings.ApiBaseUrl;
        lblDbPath.Text = _settings.DbIsamPath.Length > 0 ? _settings.DbIsamPath : "(not set)";
        chkAutoSync.Checked = _settings.AutoSyncEnabled;
        nudInterval.Value = _settings.SyncIntervalSeconds;
        UpdateLastSyncLabel();
    }

    // ── Stage dropdown ────────────────────────────────────────────────────────

    private void LoadStagesFromDb()
    {
        cmbDay.Items.Clear();
        lblDayNote.Text = "";
        cmbDay.Enabled = false;

        if (string.IsNullOrWhiteSpace(_settings.DbIsamPath)) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        int todayIndex = -1;

        try
        {
            using var db = new DbBridgeService();
            if (!db.Open(_settings.DbIsamPath)) return;

            for (int dayNo = 1; dayNo <= 6; dayNo++)
            {
                var (result, info) = db.GetEtapInfo(dayNo);
                if (!result.Success) continue;
                cmbDay.Items.Add(new EtapItem(dayNo, info!.Name, info.Date));
                if (todayIndex < 0 && TryParseEtapDate(info.Date, out var d) && d == today)
                    todayIndex = cmbDay.Items.Count - 1;
            }
        }
        catch { /* DB unavailable — leave combo empty */ }

        if (cmbDay.Items.Count == 0) return;

        cmbDay.Enabled = true;

        if (todayIndex >= 0)
        {
            cmbDay.SelectedIndex = todayIndex;
            lblDayNote.Text = "";
        }
        else
        {
            // Default to stage whose DayNo matches saved setting, or first stage
            int savedIndex = -1;
            for (int i = 0; i < cmbDay.Items.Count; i++)
            {
                if (((EtapItem)cmbDay.Items[i]!).DayNo == _settings.DayNo)
                { savedIndex = i; break; }
            }
            cmbDay.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;
            lblDayNote.Text = $"Competition does not have a stage with today's date ({today:yyyy-MM-dd}).";
        }
    }

    private static bool TryParseEtapDate(string s, out DateOnly date)
    {
        s = s.Trim();
        string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "MM/dd/yyyy" };
        foreach (var fmt in formats)
            if (DateOnly.TryParseExact(s, fmt, null, System.Globalization.DateTimeStyles.None, out date))
                return true;
        return DateOnly.TryParse(s, out date);
    }

    private void cmbDay_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbDay.SelectedItem is EtapItem item)
        {
            _settings.DayNo = item.DayNo;
            _settings.Save();
        }
    }

    // ── Log / status ──────────────────────────────────────────────────────────

    private void UpdateLastSyncLabel()
    {
        lblLastSync.Text = _settings.LastServerTimeUtc == DateTimeOffset.MinValue
            ? "Never"
            : _settings.LastServerTimeUtc.ToLocalTime().ToString("HH:mm:ss");
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.ScrollToCaret();
        UpdateLastSyncLabel();
        try { File.AppendAllText(_logFilePath, message + Environment.NewLine); } catch { }
    }

    private void UpdateStatusLabel(string status)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatusLabel(status)); return; }
        lblStatus.Text = $"Status: {status}";
    }

    // ── Buttons / handlers ────────────────────────────────────────────────────

    private async void btnSyncNow_Click(object sender, EventArgs e)
    {
        btnSyncNow.Enabled = false;
        UpdateStatusLabel("Syncing...");
        try { await _syncService.RunCycleAsync(forcePush: false); }
        finally { btnSyncNow.Enabled = true; UpdateStatusLabel("Idle"); }
    }

    private async void btnForcePush_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show(
                "Force Push All will overwrite all non-status fields on the server with current DBISAM values.\n\nContinue?",
                "Confirm Force Push",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        btnForcePush.Enabled = false;
        UpdateStatusLabel("Force pushing...");
        try
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Force Push All initiated.");
            await _syncService.RunCycleAsync(forcePush: true);
        }
        finally { btnForcePush.Enabled = true; UpdateStatusLabel("Idle"); }
    }

    private void txtApiUrl_Leave(object sender, EventArgs e)
    {
        _settings.ApiBaseUrl = txtApiUrl.Text.Trim();
        _settings.Save();
    }

    private void btnEditApi_Click(object sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _settings = dlg.Result;
        _settings.Save();
        _syncService.UpdateInterval(_settings.SyncIntervalSeconds);
        LoadSettingsToUi();
    }

    private void btnBrowseDb_Click(object sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select DBISAM database folder",
            SelectedPath = _settings.DbIsamPath
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _settings.DbIsamPath = dlg.SelectedPath;
        _settings.Save();
        lblDbPath.Text = _settings.DbIsamPath;
        LoadStagesFromDb();
    }

    private void btnDbExplorer_Click(object sender, EventArgs e)
    {
        using var form = new DbBridgeExplorerForm();
        form.ShowDialog(this);
    }

    private void chkAutoSync_CheckedChanged(object sender, EventArgs e)
    {
        _settings.AutoSyncEnabled = chkAutoSync.Checked;
        _settings.Save();
        if (_settings.AutoSyncEnabled) _syncService.Start(); else _syncService.Stop();
    }

    private void nudInterval_ValueChanged(object sender, EventArgs e)
    {
        _settings.SyncIntervalSeconds = (int)nudInterval.Value;
        _settings.Save();
        _syncService.UpdateInterval(_settings.SyncIntervalSeconds);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Flush any pending textbox edits before exit
        _settings.ApiBaseUrl = txtApiUrl.Text.Trim();
        _settings.Save();
        base.OnFormClosing(e);
    }
}
