namespace StartRef.Desktop;

/// <summary>
/// Standalone dialog for manually testing every DbBridge DLL function and exploring raw data.
/// Browse immediately opens the DB and populates the stage dropdown.
/// Day selector shows actual stage names; today's date is auto-selected.
/// </summary>
public partial class DbBridgeExplorerForm : Form
{
    private DbBridgeService? _db;

    // Top: DB path + controls
    private TextBox _txtDbPath = null!;
    private Button _btnBrowseDb = null!, _btnOpen = null!, _btnClose = null!;
    private Label _lblStatus = null!, _lblDayNote = null!;
    private ComboBox _cmbDay = null!;

    // Shared lookup inputs
    private TextBox _txtIdNr = null!, _txtStartNr = null!, _txtChipSearch = null!;

    // Common value inputs
    private TextBox _txtTime = null!, _txtNewChip = null!, _txtNewKat = null!;

    // Output
    private TextBox _txtLog = null!;
    private Button _btnClearLog = null!;

    // Stores DayNo per combo item
    private sealed record EtapItem(int DayNo, string Name, string Date, int Nullzeit)
    {
        public override string ToString() => $"Day {DayNo}: {Name}  ({Date})";
    }

    public DbBridgeExplorerForm()
    {
        InitializeComponent();
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        UpdateStatus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _db?.Close();
        _db?.Dispose();
        _db = null;
        base.OnFormClosing(e);
    }

    // ── DB lifecycle ─────────────────────────────────────────────────────────

    private void BtnBrowseDb_Click(object sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select DBISAM database folder",
            SelectedPath = _txtDbPath.Text
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _txtDbPath.Text = dlg.SelectedPath;
        OpenDb();
    }

    private void BtnOpen_Click(object sender, EventArgs e) => OpenDb();

    private void BtnClose_Click(object sender, EventArgs e)
    {
        _db?.Close();
        Log("DB CLOSED");
        _cmbDay.Items.Clear();
        _lblDayNote.Visible = false;
        UpdateStatus();
    }

    private void OpenDb()
    {
        if (_db?.IsOpen == true)
        {
            _db.Close();
        }
        _db?.Dispose();
        _db = new DbBridgeService(Log);

        if (_db.Open(_txtDbPath.Text.Trim()))
        {
            Log("DB OPEN OK");
            LoadEtapInfo();
        }
        else
        {
            Log("DB OPEN FAILED — check path, DLL, and that app runs as x86.");
        }
        UpdateStatus();
    }

    private void LoadEtapInfo()
    {
        _cmbDay.Items.Clear();
        _lblDayNote.Visible = false;

        var today = DateOnly.FromDateTime(DateTime.Today);
        int todayIndex = -1;

        for (int dayNo = 1; dayNo <= 6; dayNo++)
        {
            var (result, info) = _db!.GetEtapInfo(dayNo);
            if (!result.Success) continue;

            var item = new EtapItem(dayNo, info!.Name, info.Date, info.Nullzeit);
            _cmbDay.Items.Add(item);
            Log($"  Stage {dayNo}: {info.Name}  {info.Date}  Nullzeit={info.NullzeitFormatted}");

            if (todayIndex < 0 && TryParseDate(info.Date, out var etapDate) && etapDate == today)
                todayIndex = _cmbDay.Items.Count - 1;
        }

        if (_cmbDay.Items.Count == 0)
        {
            Log("[WARN] No stage info found — DB may be empty or day config not set.");
            return;
        }

        if (todayIndex >= 0)
        {
            _cmbDay.SelectedIndex = todayIndex;
        }
        else
        {
            _cmbDay.SelectedIndex = 0;
            _lblDayNote.Text = "Competition does not have a stage with today's date.";
            _lblDayNote.Visible = true;
            Log($"[WARN] No stage matches today ({today:yyyy-MM-dd}). Defaulted to first stage.");
        }
    }

    private void UpdateStatus()
    {
        bool open = _db?.IsOpen == true;
        _lblStatus.Text = open ? "Status: Open" : "Status: Closed";
        _lblStatus.ForeColor = open ? System.Drawing.Color.Green : System.Drawing.Color.Red;
        _btnClose.Enabled = open;
        _cmbDay.Enabled = open && _cmbDay.Items.Count > 0;
    }

    private int Day => (_cmbDay.SelectedItem as EtapItem)?.DayNo ?? 1;

    private static bool TryParseDate(string s, out DateOnly date)
    {
        s = s.Trim();
        string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "MM/dd/yyyy" };
        foreach (var fmt in formats)
        {
            if (DateOnly.TryParseExact(s, fmt, null, System.Globalization.DateTimeStyles.None, out date))
                return true;
        }
        return DateOnly.TryParse(s, out date);
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    private void BtnGetEtapInfo_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        var (result, info) = _db!.GetEtapInfo(Day);
        if (result.Success)
        {
            LogSection($"GetEtapInfo(day={Day})",
                $"Name:     {info!.Name}",
                $"Date:     {info.Date}",
                $"Nullzeit: {info.Nullzeit} raw → {info.NullzeitFormatted}");
        }
        else LogResult("GetEtapInfo", result);
    }

    private void BtnGetTeilnInfo_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        var (result, raw) = _db!.GetTeilnInfoByIdNr(ParseInt(_txtIdNr.Text));
        if (result.Success)
            LogSection($"GetTeilnInfoByIdNr(idNr={_txtIdNr.Text})", raw!);
        else LogResult("GetTeilnInfoByIdNr", result);
    }

    private void BtnGetIdNrByStartNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        var (result, raw) = _db!.GetIdNrListByStartNr(ParseInt(_txtStartNr.Text));
        if (result.Success)
            LogSection($"GetIdNrListByStartNr(startNr={_txtStartNr.Text})", raw!);
        else LogResult("GetIdNrListByStartNr", result);
    }

    private void BtnGetIdNrByChipNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        var (result, raw) = _db!.GetIdNrListByChipNr(Day, ParseInt(_txtChipSearch.Text));
        if (result.Success)
            LogSection($"GetIdNrListByChipNr(day={Day}, chipNr={_txtChipSearch.Text})", raw!);
        else LogResult("GetIdNrListByChipNr", result);
    }

    // ── Write: StartTime ─────────────────────────────────────────────────────

    private void BtnChangeStartById_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeStartTimeByIdNr", _db!.ChangeStartTimeByIdNr(Day, _txtTime.Text.Trim(), ParseInt(_txtIdNr.Text)));
    }

    private void BtnChangeStartByStartNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeStartTimeByStartNr", _db!.ChangeStartTimeByStartNr(Day, _txtTime.Text.Trim(), ParseInt(_txtStartNr.Text)));
    }

    private void BtnChangeStartByChipNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeStartTimeByChipNr", _db!.ChangeStartTimeByChipNr(Day, _txtTime.Text.Trim(), ParseInt(_txtChipSearch.Text)));
    }

    // ── Write: ChipNr ────────────────────────────────────────────────────────

    private void BtnChangeChipById_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeChipNrByIdNr", _db!.ChangeChipNrByIdNr(Day, ParseInt(_txtNewChip.Text), ParseInt(_txtIdNr.Text)));
    }

    private void BtnChangeChipByStartNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeChipNrByStartNr", _db!.ChangeChipNrByStartNr(Day, ParseInt(_txtNewChip.Text), ParseInt(_txtStartNr.Text)));
    }

    private void BtnChangeChipByOldChip_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeChipNrByOldChipNr", _db!.ChangeChipNrByOldChipNr(Day, ParseInt(_txtNewChip.Text), ParseInt(_txtChipSearch.Text)));
    }

    // ── Write: KatNr ─────────────────────────────────────────────────────────

    private void BtnChangeKatById_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeKatNrByIdNr", _db!.ChangeKatNrByIdNr(ParseInt(_txtNewKat.Text), ParseInt(_txtIdNr.Text)));
    }

    private void BtnChangeKatByStartNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeKatNrByStartNr", _db!.ChangeKatNrByStartNr(ParseInt(_txtNewKat.Text), ParseInt(_txtStartNr.Text)));
    }

    private void BtnChangeKatByChipNr_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("ChangeKatNrByChipNr", _db!.ChangeKatNrByChipNr(Day, ParseInt(_txtNewKat.Text), ParseInt(_txtChipSearch.Text)));
    }

    // ── Test mode ────────────────────────────────────────────────────────────

    private void BtnEnableTestMode_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("SetTestMode", _db!.SetTestMode(Day));
    }

    private void BtnDisableTestMode_Click(object sender, EventArgs e)
    {
        if (!EnsureOpen()) return;
        LogResult("DisableTestMode", _db!.DisableTestMode());
    }

    // ── Output helpers ───────────────────────────────────────────────────────

    private void BtnClearLog_Click(object sender, EventArgs e) => _txtLog.Clear();

    private bool EnsureOpen()
    {
        if (_db?.IsOpen == true) return true;
        Log("DB is not open. Use Browse or Open button first.");
        return false;
    }

    private void Log(string text)
    {
        if (_txtLog.InvokeRequired) { _txtLog.Invoke(() => Log(text)); return; }
        _txtLog.AppendText($"{DateTime.Now:HH:mm:ss} | {text}{Environment.NewLine}");
        _txtLog.ScrollToCaret();
    }

    private void LogSection(string title, params string[] lines)
    {
        var sep = new string('-', 50);
        Log(sep);
        Log(title);
        foreach (var line in lines)
            Log("  " + line);
        Log(sep);
    }

    private void LogResult(string op, DbBridgeResult result)
    {
        if (result.Success)
            Log($"{op}: OK — {result.Message}");
        else
            Log($"{op}: ERROR [{result.Code}] {result.Message}");
    }

    private static int ParseInt(string s) => int.TryParse(s.Trim(), out int v) ? v : 0;
}
