namespace StartRef.Desktop.Forms;

partial class DbBridgeExplorerForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Text = "DbBridge Explorer";
        Size = new System.Drawing.Size(980, 980);
        MinimumSize = new System.Drawing.Size(940, 900);
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterParent;
        Font = new System.Drawing.Font("Segoe UI", 9f);

        int y = 10;
        const int lw = 65, vw = 480, bw = 65, rowH = 30;

        // ── DB Path ───────────────────────────────────────────────────────────
        AddLabel("DB Path:", 10, y, lw);
        _txtDbPath = AddTextBox(lw + 5, y, vw);
        _btnBrowseDb = AddButton("Browse", lw + vw + 10, y, bw, BtnBrowseDb_Click);
        _btnOpen = AddButton("Open", lw + vw + bw + 15, y, bw, BtnOpen_Click);
        _btnClose = AddButton("Close", lw + vw + bw * 2 + 20, y, bw, BtnClose_Click);
        y += rowH;

        // Status
        _lblStatus = AddLabel("Status: Closed", 10, y, 160);
        _lblStatus.ForeColor = System.Drawing.Color.Red;
        y += 24;

        // Day selector (populated after DB open)
        AddLabel("Stage:", 10, y, 42);
        _cmbDay = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(55, y),
            Width = 420,
            Enabled = false
        };
        Controls.Add(_cmbDay);
        y += rowH;

        // Day note label (shown in red when today's date isn't in the competition)
        _lblDayNote = new Label
        {
            Text = "",
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(830, 18),
            ForeColor = System.Drawing.Color.Red,
            Visible = false
        };
        Controls.Add(_lblDayNote);
        y += 22;

        // ── Shared lookup inputs ──────────────────────────────────────────────
        AddSep(y); y += 8;
        AddLabel("Shared lookup inputs:", 10, y, 140);
        y += 22;

        AddLabel("IdNr:", 10, y, 38);
        _txtIdNr = AddTextBox(50, y, 85);
        AddLabel("StartNr:", 200, y, 52);
        _txtStartNr = AddTextBox(255, y, 85);
        AddLabel("ChipNr:", 400, y, 48);
        _txtChipSearch = AddTextBox(452, y, 85);
        y += rowH;

        AddLabel("Time (hh:mm:ss):", 10, y, 115);
        _txtTime = AddTextBox(128, y, 85);
        _txtTime.Text = "10:00:00";
        AddLabel("New ChipNr:", 280, y, 78);
        _txtNewChip = AddTextBox(362, y, 80);
        AddLabel("New KatNr:", 490, y, 68);
        _txtNewKat = AddTextBox(562, y, 70);
        y += rowH;

        AddLabel("New Name:", 10, y, 78);
        _txtNewName = AddTextBox(92, y, 300);
        y += rowH + 5;

        // ── Read ──────────────────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Read", 10, y); y += 24;

        AddButton("Get Teiln Info by IdNr", 10, y, 180, BtnGetTeilnInfo_Click);
        AddButton("Get IdNr List by StartNr", 200, y, 195, BtnGetIdNrByStartNr_Click);
        AddButton("Get IdNr List by ChipNr", 405, y, 195, BtnGetIdNrByChipNr_Click);
        y += rowH + 5;

        // ── Write: StartTime ──────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Write: StartTime  (uses Time input above)", 10, y); y += 24;

        AddButton("Change StartTime by IdNr", 10, y, 195, BtnChangeStartById_Click);
        AddButton("Change StartTime by StartNr", 215, y, 210, BtnChangeStartByStartNr_Click);
        AddButton("Change StartTime by ChipNr", 435, y, 210, BtnChangeStartByChipNr_Click);
        y += rowH + 5;

        // ── Write: ChipNr ─────────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Write: ChipNr  (uses New ChipNr input above)", 10, y); y += 24;

        AddButton("Change ChipNr by IdNr", 10, y, 185, BtnChangeChipById_Click);
        AddButton("Change ChipNr by StartNr", 205, y, 200, BtnChangeChipByStartNr_Click);
        AddButton("Change ChipNr by OldChip", 415, y, 205, BtnChangeChipByOldChip_Click);
        y += rowH + 5;

        // ── Write: KatNr ──────────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Write: KatNr  (uses New KatNr input above)", 10, y); y += 24;

        AddButton("Change KatNr by IdNr", 10, y, 180, BtnChangeKatById_Click);
        AddButton("Change KatNr by StartNr", 200, y, 195, BtnChangeKatByStartNr_Click);
        AddButton("Change KatNr by ChipNr", 405, y, 195, BtnChangeKatByChipNr_Click);
        y += rowH + 5;

        // ── Write: Name ───────────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Write: Name  (uses IdNr + New Name inputs above)", 10, y); y += 24;

        AddButton("Update Name by IdNr", 10, y, 190, BtnUpdateName_Click);
        y += rowH + 5;

        // ── Test Mode ─────────────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Test Mode", 10, y); y += 24;

        AddButton("Enable Test Mode", 10, y, 150, BtnEnableTestMode_Click);
        AddButton("Disable Test Mode", 170, y, 155, BtnDisableTestMode_Click);
        y += rowH + 5;

        // ── Output ────────────────────────────────────────────────────────────
        AddSep(y); y += 8;
        AddSectionLabel("Output", 10, y); y += 24;

        var outputTop = y;
        var outputHeight = Math.Max(170, ClientSize.Height - outputTop - 65);
        _txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new System.Drawing.Font("Consolas", 8.5f),
            Location = new System.Drawing.Point(10, outputTop),
            Size = new System.Drawing.Size(830, outputHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        Controls.Add(_txtLog);
        y = outputTop + outputHeight + 8;

        _btnClearLog = AddButton("Clear Log", 10, y, 90, BtnClearLog_Click);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private Label AddLabel(string text, int x, int y, int w)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new System.Drawing.Point(x, y + 2),
            Size = new System.Drawing.Size(w, 18),
            AutoSize = false
        };
        Controls.Add(lbl);
        return lbl;
    }

    private Label AddSectionLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = "=== " + text + " ===",
            Location = new System.Drawing.Point(x, y),
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
        };
        Controls.Add(lbl);
        return lbl;
    }

    private TextBox AddTextBox(int x, int y, int w)
    {
        var tb = new TextBox
        {
            Location = new System.Drawing.Point(x, y),
            Width = w
        };
        Controls.Add(tb);
        return tb;
    }

    private Button AddButton(string text, int x, int y, int w, EventHandler handler)
    {
        var btn = new Button
        {
            Text = text,
            Location = new System.Drawing.Point(x, y),
            Size = new System.Drawing.Size(w, 24)
        };
        btn.Click += handler;
        Controls.Add(btn);
        return btn;
    }

    private void AddSep(int y)
    {
        var sep = new Label
        {
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(830, 2),
            BorderStyle = BorderStyle.Fixed3D
        };
        Controls.Add(sep);
    }
}
