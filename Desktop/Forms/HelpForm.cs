namespace StartRef.Desktop.Forms;

public class HelpForm : Form
{
    public HelpForm()
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Text = "StartRef Desktop — Help";
        Size = new System.Drawing.Size(820, 700);
        MinimumSize = new System.Drawing.Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;

        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = System.Drawing.SystemColors.Window,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true
        };

        Controls.Add(rtb);
        Load += (_, _) => PopulateHelp(rtb);
    }

    private static void PopulateHelp(RichTextBox r)
    {
        var bodyFont    = new System.Drawing.Font("Segoe UI", 9.5f);
        var headingFont = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold);
        var subFont     = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold);
        var monoFont    = new System.Drawing.Font("Consolas", 8.75f);
        var headingColor = System.Drawing.Color.FromArgb(0, 70, 130);
        var warningColor = System.Drawing.Color.FromArgb(180, 40, 0);
        var dimColor     = System.Drawing.Color.FromArgb(90, 90, 90);

        void Heading(string text)
        {
            r.SelectionFont  = headingFont;
            r.SelectionColor = headingColor;
            r.AppendText(text + "\n");
            r.SelectionFont  = bodyFont;
            r.SelectionColor = System.Drawing.Color.Black;
        }

        void Sub(string label, string body = "")
        {
            r.SelectionFont  = subFont;
            r.SelectionColor = System.Drawing.Color.Black;
            r.AppendText("  " + label);
            if (body.Length > 0)
            {
                r.SelectionFont  = bodyFont;
                r.SelectionColor = System.Drawing.Color.Black;
                r.AppendText("  —  " + body);
            }
            r.AppendText("\n");
        }

        void Body(string text)
        {
            r.SelectionFont  = bodyFont;
            r.SelectionColor = System.Drawing.Color.Black;
            r.AppendText(text + "\n");
        }

        void Mono(string text)
        {
            r.SelectionFont  = monoFont;
            r.SelectionColor = dimColor;
            r.AppendText(text + "\n");
        }

        void Warn(string text)
        {
            r.SelectionFont  = subFont;
            r.SelectionColor = warningColor;
            r.AppendText("  " + text + "\n");
            r.SelectionFont  = bodyFont;
            r.SelectionColor = System.Drawing.Color.Black;
        }

        void Nl() => r.AppendText("\n");

        // ── Overview ──────────────────────────────────────────────────────────
        Heading("Overview");
        Body("StartRef Desktop synchronises orienteering competition data between a local DBISAM\n" +
             "database and the StartRef web API.  Field devices\n" +
             "(tablets, phones) connect to the same API and share start-list changes in real time.\n" +
             "This desktop app acts as the bridge: it reads and writes the DBISAM files and\n" +
             "pushes/pulls updates to/from the API.");
        Nl();

        // ── Configuration ─────────────────────────────────────────────────────
        Heading("Configuration");

        Sub("API URL",
            "Base URL of the StartRef web API (e.g. https://startref.azurewebsites.net/).\n" +
            "  Saved automatically when you leave the field.");
        Nl();
        Sub("Settings (button)",
            "Opens a dialog to configure:\n" +
            "    API Key      — secret key sent in every API request header.\n" +
            "    Device Name  — identifies this desktop in logs and change tracking.\n" +
            "                   Keep it unique per device (default: \"desktop\").");
        Nl();
        Sub("🔊 / 🔇 Sound toggle",
            "Enables or disables an audible beep when an error is detected in the log.\n" +
            "  Trigger keywords: \"failed\", \"error\", \"unreachable\", \"not available\".");
        Nl();
        Sub("DB Path",
            "Folder containing the DBISAM database files (.tbl, .idx, …).\n" +
            "  Click Browse to select.  The app reads from and writes back to these files.");
        Nl();
        Sub("Stage",
            "Select the competition stage (day) loaded from DBISAM.\n" +
            "  Shows: Day N — Stage name (date).");
        Nl();
        Sub("API Date",
            "Calendar date sent to the API.  Set automatically from the selected stage.\n" +
            "  A red warning appears if it does not match any stage in the database.");
        Nl();

        // ── Auto-Pull ─────────────────────────────────────────────────────────
        Heading("Auto-Pull  (receiving changes)");

        Sub("Auto-pull enabled",
            "Polls the API at the configured interval and writes changes from other\n" +
            "  devices (tablets, other desktops) back into DBISAM.");
        Nl();
        Sub("Interval (s)",
            "How often to poll, in seconds (10–3600).  Default: 30 s.");
        Nl();
        Sub("Last sync",
            "Timestamp of the most recent completed pull cycle.");
        Nl();
        Body("  Fields updated by auto-pull:  Surname, Name, Club, Class, Chip number,\n" +
             "  Start time, DNS status.\n\n" +
             "  The app only writes to DBISAM if the change came from a different device.\n" +
             "  Start times before the Nullzeit offset (race start) are rejected.\n" +
             "  Chip numbers are propagated across competition days 1–3.");
        Nl();

        // ── Auto-Push ─────────────────────────────────────────────────────────
        Heading("Auto-Push  (sending changes)");

        Sub("Auto-push enabled",
            "Periodically uploads DBISAM changes to the API.  Compares the current\n" +
            "  DBISAM state against the API and only sends rows that differ.");
        Nl();
        Sub("Interval (s)",
            "How often to push, in seconds (10–3600).  Default: 600 s (10 min).");
        Nl();
        Sub("Last push",
            "Timestamp of the most recent completed push cycle.");
        Nl();
        Body("  Auto-push waits if an auto-pull is already running to prevent\n" +
             "  concurrent database access.");
        Nl();

        // ── Manual Operations ─────────────────────────────────────────────────
        Heading("Manual Operations");

        Sub("Pull Now",
            "Runs one pull cycle immediately.  Fetches only changes since the last\n" +
            "  watermark timestamp (delta sync).");
        Nl();
        Sub("Push Updates",
            "Compares every DBISAM runner against the API and uploads rows that differ.\n" +
            "  The API applies only real field-level diffs; unchanged values are not touched.");
        Nl();
        Sub("Pull Past",
            "Opens a dialog to pull all API changes from the last N minutes (1–240).\n" +
            "  Useful after a network outage to retrieve missed updates.");
        Nl();
        Sub("Reset Day",
            "Full four-step resync for the selected date:\n" +
            "    1. Delete all API data for the date.\n" +
            "    2. Upload all clubs from DBISAM.\n" +
            "    3. Upload all classes from DBISAM.\n" +
            "    4. Force-push all runners from DBISAM.\n" +
            "  Clears the watermark so the next auto-pull starts fresh.\n" +
            "  Use this to recover from corrupted or stale API data.");
        Nl();
        Sub("Peek API data",
            "Queries the API and shows the current count of competitors, clubs, and\n" +
            "  classes for the selected date.  Read-only — no changes are made.");
        Nl();

        // ── Advanced ──────────────────────────────────────────────────────────
        Heading("Advanced Operations  (▶ Advanced)");

        Sub("Force Push All",
            "Uploads all runners for the selected stage with touchAll=true.\n" +
            "  Overwrites every field on the API with values from DBISAM, even if\n" +
            "  the API has a newer value.  Use with caution.");
        Nl();
        Sub("Push selected",
            "Same as Force Push All but for an inclusive range of start numbers.\n" +
            "  Opens a dialog to enter From / To start numbers.");
        Nl();
        Sub("Push Clubs",   "Uploads the club lookup table from DBISAM to the API (upsert).");
        Nl();
        Sub("Push Classes", "Uploads the class/category lookup table from DBISAM to the API (upsert).");
        Nl();
        Warn("Delete Today  ⚠");
        Body("    Deletes ALL competitor, club, and class data from the API for the\n" +
             "    selected date.  Requires double confirmation.  Cannot be undone from\n" +
             "    the desktop; use Reset Day to restore data from DBISAM.");
        Nl();
        Sub("DB Explorer (E)",
            "Low-level database explorer window.  Look up participants by start number\n" +
            "  or chip number and inspect raw DBISAM record fields.\n" +
            "  Useful for debugging data discrepancies.");
        Nl();

        // ── Test Mode ─────────────────────────────────────────────────────────
        Heading("Test Mode");
        Warn("Test mode  ⚠  Dangerous");
        Body("    Normally the app refuses to write to a DBISAM database for a date other\n" +
             "    than the current competition date.  Test mode disables this guard, allowing\n" +
             "    writes to any date.  A warning dialog is shown when you enable it.\n" +
             "    Disable it as soon as you are done with testing.");
        Nl();

        // ── DBISAM ────────────────────────────────────────────────────────────
        Heading("DBISAM Database");
        Body("The app accesses DBISAM via a native 32-bit library (DbBridge.dll).\n" +
             "This is why the application must run as a 32-bit (x86) process.\n");
        Body("  • Only one instance should access the same DBISAM folder at a time.\n" +
             "  • Default encoding: Code Page 1257 (Baltic / Lithuanian).\n" +
             "    Contact your administrator if characters display incorrectly.\n" +
             "  • Rows with the MldKen (de-registered) flag are skipped during push.\n" +
             "  • DNS status is stored in the NCKen bit field:\n" +
             "      0 = OK,  1 = DNS,  2 = DNF,  3 = MP,  4 = DQ\n");
        Body("  Field name mapping between DBISAM and the API:");
        Mono("    DBISAM \"Name\"    →  API Surname (family name)\n" +
             "    DBISAM \"Vorname\" →  API Name    (first name)\n" +
             "    DBISAM \"ChipNr{N}\"              →  Chip number for day N\n" +
             "    DBISAM \"Start{N}\"               →  Start time for day N\n" +
             "    DBISAM \"NCKen{N}\" / \"MldKen{N}\" →  Status flags for day N");
        Nl();
        Body("  Log files written by the application:");
        Mono("    StartRefSync.log                   — main operation log (exe directory)\n" +
             "    DbBridge_dll.log                   — native DLL diagnostics (exe directory)\n" +
             "    <DbPath>\\logs\\DbBridge_YYYYMMDD.log — per-day database log");
        Nl();

        // ── API ───────────────────────────────────────────────────────────────
        Heading("API Integration");
        Body("The desktop communicates with the StartRef REST API over HTTPS.\n" +
             "Every request includes the API Key in the X-Api-Key header.\n");
        Body("  Endpoints used:");
        Mono("    GET    /api/competitions/{date}/runners   pull changes (delta)\n" +
             "    PUT    /api/competitions/{date}/runners   upload runners\n" +
             "    PUT    /api/lookups/{date}/classes        upload classes\n" +
             "    PUT    /api/lookups/{date}/clubs          upload clubs\n" +
             "    GET    /api/lookups/counts/{date}         counts (Peek API data)\n" +
             "    DELETE /api/competitions/{date}/data      delete all data for date\n" +
             "    GET    /api/time                          server clock (NTP-style sync)");
        Nl();
        Body("  Resilience:\n" +
             "  Failed requests are retried up to 3 times with exponential back-off\n" +
             "  (2 s → 4 s → 8 s).  Request timeout: 30 seconds.\n" +
             "  Large uploads are compressed with gzip.\n");
        Body("  Watermark / delta sync:\n" +
             "  After each successful pull the server timestamp is saved per competition date.\n" +
             "  The next pull sends changedSince=<timestamp> so only new changes are returned.\n" +
             "  Use Pull Past to override the watermark and fetch older records.\n");
        Body("  Change attribution:\n" +
             "  Every uploaded record is tagged with this device's Device Name.\n" +
             "  Pull requests send excludeSource=<deviceName> so the app never re-downloads\n" +
             "  its own changes.");
        Nl();

        r.SelectionStart = 0;
        r.ScrollToCaret();
    }
}
