using System.Runtime.InteropServices;
using System.Text;

namespace StartRef.Desktop.DbBridge;

/// <summary>
/// Managed wrapper around DbBridgeNative P/Invoke calls.
/// Holds a single context handle (CtxHandle). Call Open() before any operation, Close() when done.
/// Implements IDisposable — use with <c>using</c> to guarantee Close is called.
/// </summary>
public class DbBridgeService : IDisposable
{
    private const int DefaultDbCodePage = 1257;
    private static readonly object EncodingLock = new();
    private static Encoding _dbEncoding = CreateEncoding(DefaultDbCodePage);

    private IntPtr _ctx = IntPtr.Zero;
    private string _dataDir = string.Empty;
    private readonly Action<string>? _log;

    public bool IsOpen => _ctx != IntPtr.Zero;

    public static string GlobalDllLogPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbBridge_dll.log");

    public static string DbErrorLogPath(string dataDir, DateTime? date = null)
    {
        var logDate = (date ?? DateTime.Now).ToString("yyyyMMdd");
        return Path.Combine(dataDir, "logs", $"DbBridge_{logDate}.log");
    }

    public DbBridgeService(Action<string>? log = null)
    {
        _log = log;
    }

    public static void SetCodePage(int codePage)
    {
        lock (EncodingLock)
        {
            _dbEncoding = CreateEncoding(codePage);
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>Opens the DBISAM database at <paramref name="dataDir"/>. Returns true on success.</summary>
    public bool Open(string dataDir)
    {
        if (IsOpen) return true;
        _dataDir = dataDir;
        try
        {
            _ctx = DbBridgeNative.DbOpen(dataDir);
            if (_ctx == IntPtr.Zero)
            {
                _log?.Invoke("DbOpen returned NULL — check path and DLL dependencies.");
                _log?.Invoke($"Check DLL logs: {GlobalDllLogPath}");
                _log?.Invoke($"Check DB log: {DbErrorLogPath(_dataDir)}");
                return false;
            }
            _log?.Invoke("DB opened.");
            return true;
        }
        catch (DllNotFoundException ex)
        {
            _log?.Invoke($"DbBridge.dll not found: {ex.Message}");
            _log?.Invoke($"Check DLL logs: {GlobalDllLogPath}");
            return false;
        }
        catch (BadImageFormatException ex)
        {
            _log?.Invoke($"Architecture mismatch (app must run as x86): {ex.Message}");
            _log?.Invoke($"Check DLL logs: {GlobalDllLogPath}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"DbOpen exception: {ex.Message}");
            _log?.Invoke($"Check DLL logs: {GlobalDllLogPath}");
            _log?.Invoke($"Check DB log: {DbErrorLogPath(_dataDir)}");
            return false;
        }
    }

    public void Close()
    {
        if (!IsOpen) return;
        try
        {
            DbBridgeNative.DbClose(_ctx);
            _log?.Invoke("DB connection closed (normal cleanup).");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"DbClose exception: {ex.Message}");
        }
        finally
        {
            _ctx = IntPtr.Zero;
        }
    }

    public void Dispose() => Close();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string GetLastError()
    {
        if (!IsOpen) return string.Empty;
        var sb = new StringBuilder(1024);
        try { DbBridgeNative.DbGetLastError(_ctx, sb, sb.Capacity); }
        catch { /* ignore */ }
        return sb.ToString();
    }

    private static Encoding CreateEncoding(int codePage)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(codePage);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static string DecodeBuffer(byte[] buffer)
    {
        int len = Array.IndexOf(buffer, (byte)0);
        if (len < 0) len = buffer.Length;
        lock (EncodingLock)
        {
            return _dbEncoding.GetString(buffer, 0, len);
        }
    }

    private static byte[] EncodeString(string value)
    {
        var source = value ?? string.Empty;
        byte[] encoded;
        lock (EncodingLock)
        {
            encoded = _dbEncoding.GetBytes(source);
        }

        var buffer = new byte[encoded.Length + 1];
        Array.Copy(encoded, buffer, encoded.Length);
        return buffer;
    }

    private DbBridgeResult Ok(string message) => new(true, DbBridgeNative.DBR_OK, message);

    private DbBridgeResult Fail(int code)
    {
        var err = GetLastError();
        var root = string.IsNullOrWhiteSpace(err) ? CodeName(code) : err;
        var msg = _dataDir.Length > 0
            ? $"{root} | Logs: {GlobalDllLogPath}; {DbErrorLogPath(_dataDir)}"
            : $"{root} | Log: {GlobalDllLogPath}";
        return new(false, code, msg);
    }

    private DbBridgeResult Wrap(int code, string okMessage) =>
        code == DbBridgeNative.DBR_OK ? Ok(okMessage) : Fail(code);

    private DbBridgeResult NotOpen() => new(false, DbBridgeNative.DBR_CTX_NIL, "DB is not open.");

    public bool TestMode { get; set; } = false;

    private DbBridgeResult WrapWrite(int dayNo, string okMessage, Func<int> action)
    {
        if (!TestMode)
            return Wrap(action(), okMessage);

        int testModeCode = DbBridgeNative.DbSetTestMode(_ctx, dayNo);
        if (testModeCode != DbBridgeNative.DBR_OK)
            return Fail(testModeCode);

        try
        {
            return Wrap(action(), okMessage);
        }
        finally
        {
            try { DbBridgeNative.DbDisableTestMode(_ctx); } catch { }
        }
    }

    private static string CodeName(int code) => code switch
    {
        DbBridgeNative.DBR_ERROR => "General error",
        DbBridgeNative.DBR_NOT_FOUND => "Not found",
        DbBridgeNative.DBR_INVALID_DAY => "Invalid day (must be 1–6)",
        DbBridgeNative.DBR_DAY_NOT_ALLOWED => "Day not allowed — enable test mode first",
        DbBridgeNative.DBR_INVALID_TIME => "Invalid time format (expected hh:mm:ss)",
        DbBridgeNative.DBR_CTX_NIL => "Context is null",
        DbBridgeNative.DBR_MULTIPLE_MATCHES => "Multiple records matched",
        _ => $"Unknown code {code}"
    };

    // ── Etap / Config ────────────────────────────────────────────────────────

    public (DbBridgeResult Result, DbEtapInfo? Info) GetEtapInfo(int dayNo)
    {
        if (!IsOpen) return (NotOpen(), null);
        var nameBuf = new StringBuilder(256);
        var dateBuf = new StringBuilder(256);
        int code = DbBridgeNative.DbGetEtapInfo(_ctx, dayNo, nameBuf, nameBuf.Capacity, dateBuf, dateBuf.Capacity, out int nullzeit);
        var result = Wrap(code, "OK");
        if (!result.Success) return (result, null);
        return (result, new DbEtapInfo(nameBuf.ToString(), dateBuf.ToString(), nullzeit));
    }

    // ── Test mode ────────────────────────────────────────────────────────────

    public DbBridgeResult SetTestMode(int dayNo) =>
        IsOpen ? Wrap(DbBridgeNative.DbSetTestMode(_ctx, dayNo), "Test mode enabled") : NotOpen();

    public DbBridgeResult DisableTestMode() =>
        IsOpen ? Wrap(DbBridgeNative.DbDisableTestMode(_ctx), "Test mode disabled") : NotOpen();

    // ── Read ─────────────────────────────────────────────────────────────────

    public (DbBridgeResult Result, string? Raw) GetTeilnInfoByIdNr(int idNr)
    {
        if (!IsOpen) return (NotOpen(), null);
        var buf = new StringBuilder(8192);
        int code = DbBridgeNative.DbGetTeilnInfoByIdNr(_ctx, idNr, buf, buf.Capacity);
        var result = Wrap(code, "OK");
        return (result, result.Success ? buf.ToString() : null);
    }

    public (DbBridgeResult Result, string? Raw) GetIdNrListByStartNr(int startNr)
    {
        if (!IsOpen) return (NotOpen(), null);
        var buf = new StringBuilder(8192);
        int code = DbBridgeNative.DbGetIdNrListByStartNr(_ctx, startNr, buf, buf.Capacity);
        var result = Wrap(code, "OK");
        return (result, result.Success ? buf.ToString() : null);
    }

    public (DbBridgeResult Result, string? Raw) GetIdNrListByChipNr(int dayNo, int chipNr)
    {
        if (!IsOpen) return (NotOpen(), null);
        var buf = new StringBuilder(8192);
        int code = DbBridgeNative.DbGetIdNrListByChipNr(_ctx, dayNo, chipNr, buf, buf.Capacity);
        var result = Wrap(code, "OK");
        return (result, result.Success ? buf.ToString() : null);
    }

    // ── Change StartTime ─────────────────────────────────────────────────────

    public DbBridgeResult ChangeStartTimeByIdNr(int dayNo, string hhmmss, int idNr) =>
        IsOpen ? WrapWrite(dayNo, "StartTime updated",
            () => DbBridgeNative.DbChangeStartTimeByIdNr(_ctx, dayNo, hhmmss, idNr)) : NotOpen();

    public DbBridgeResult ChangeStartTimeByStartNr(int dayNo, string hhmmss, int startNr) =>
        IsOpen ? WrapWrite(dayNo, "StartTime updated",
            () => DbBridgeNative.DbChangeStartTimeByStartNr(_ctx, dayNo, hhmmss, startNr)) : NotOpen();

    public DbBridgeResult ChangeStartTimeByChipNr(int dayNo, string hhmmss, int chipNr) =>
        IsOpen ? WrapWrite(dayNo, "StartTime updated",
            () => DbBridgeNative.DbChangeStartTimeByChipNr(_ctx, dayNo, hhmmss, chipNr)) : NotOpen();

    // ── Change ChipNr ────────────────────────────────────────────────────────

    public DbBridgeResult ChangeChipNrByIdNr(int dayNo, int newChipNr, int idNr) =>
        IsOpen ? WrapWrite(dayNo, "ChipNr updated",
            () => DbBridgeNative.DbChangeChipNrByIdNr(_ctx, dayNo, newChipNr, idNr)) : NotOpen();

    public DbBridgeResult ChangeChipNrByStartNr(int dayNo, int newChipNr, int startNr) =>
        IsOpen ? WrapWrite(dayNo, "ChipNr updated",
            () => DbBridgeNative.DbChangeChipNrByStartNr(_ctx, dayNo, newChipNr, startNr)) : NotOpen();

    public DbBridgeResult ChangeChipNrByOldChipNr(int dayNo, int newChipNr, int oldChipNr) =>
        IsOpen ? WrapWrite(dayNo, "ChipNr updated",
            () => DbBridgeNative.DbChangeChipNrByOldChipNr(_ctx, dayNo, newChipNr, oldChipNr)) : NotOpen();

    // ── Change KatNr ─────────────────────────────────────────────────────────

    public DbBridgeResult ChangeKatNrByIdNr(int newKatNr, int idNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeKatNrByIdNr(_ctx, newKatNr, idNr), "KatNr updated") : NotOpen();

    public DbBridgeResult ChangeKatNrByStartNr(int newKatNr, int startNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeKatNrByStartNr(_ctx, newKatNr, startNr), "KatNr updated") : NotOpen();

    public DbBridgeResult ChangeKatNrByChipNr(int dayNo, int newKatNr, int chipNr) =>
        IsOpen ? WrapWrite(dayNo, "KatNr updated",
            () => DbBridgeNative.DbChangeKatNrByChipNr(_ctx, dayNo, newKatNr, chipNr)) : NotOpen();

    // ── Change Name / Vorname ────────────────────────────────────────────────
    // DBISAM "Name" = surname; "Vorname" = first name.

    public DbBridgeResult ChangeNameByIdNr(int idNr, string newName) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeNameByIdNr(_ctx, newName, idNr), "Name updated") : NotOpen();

    public DbBridgeResult ChangeNameByStartNr(int startNr, string newName) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeNameByStartNr(_ctx, newName, startNr), "Name updated") : NotOpen();

    public DbBridgeResult ChangeVornameByIdNr(int idNr, string newVorname) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeVornameByIdNr(_ctx, newVorname, idNr), "Vorname updated") : NotOpen();

    public DbBridgeResult ChangeVornameByStartNr(int startNr, string newVorname) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeVornameByStartNr(_ctx, newVorname, startNr), "Vorname updated") : NotOpen();

    public DbBridgeResult ChangeNameVornameByIdNr(int idNr, string newName, string newVorname) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeNameVornameByIdNr(_ctx, newName, newVorname, idNr), "Name+Vorname updated") : NotOpen();

    public DbBridgeResult ChangeNameVornameByStartNr(int startNr, string newName, string newVorname) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeNameVornameByStartNr(_ctx, newName, newVorname, startNr), "Name+Vorname updated") : NotOpen();

    // ── DNS (NCKen) ──────────────────────────────────────────────────────────
    // NCKen values: 0=OK, 1=DNS, 2=DNF, 3=MP, 4=DQ.
    // DNS functions use IsDayAllowed — WrapWrite applies test mode when needed.

    public DbBridgeResult SetDNSByIdNr(int dayNo, int idNr) =>
        IsOpen ? WrapWrite(dayNo, "DNS set", () => DbBridgeNative.DbSetDNSByIdNr(_ctx, dayNo, idNr)) : NotOpen();

    public DbBridgeResult ClearDNSByIdNr(int dayNo, int idNr) =>
        IsOpen ? WrapWrite(dayNo, "DNS cleared", () => DbBridgeNative.DbClearDNSByIdNr(_ctx, dayNo, idNr)) : NotOpen();

    public DbBridgeResult SetDNSByStartNr(int dayNo, int startNr) =>
        IsOpen ? WrapWrite(dayNo, "DNS set", () => DbBridgeNative.DbSetDNSByStartNr(_ctx, dayNo, startNr)) : NotOpen();

    public DbBridgeResult ClearDNSByStartNr(int dayNo, int startNr) =>
        IsOpen ? WrapWrite(dayNo, "DNS cleared", () => DbBridgeNative.DbClearDNSByStartNr(_ctx, dayNo, startNr)) : NotOpen();

    public DbBridgeResult SetDNSByChipNr(int dayNo, int chipNr) =>
        IsOpen ? WrapWrite(dayNo, "DNS set", () => DbBridgeNative.DbSetDNSByChipNr(_ctx, dayNo, chipNr)) : NotOpen();

    public DbBridgeResult ClearDNSByChipNr(int dayNo, int chipNr) =>
        IsOpen ? WrapWrite(dayNo, "DNS cleared", () => DbBridgeNative.DbClearDNSByChipNr(_ctx, dayNo, chipNr)) : NotOpen();

    // ── Change ClubNr ────────────────────────────────────────────────────────

    public DbBridgeResult ChangeClubNrByIdNr(int newClubNr, int idNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeClubNrByIdNr(_ctx, newClubNr, idNr), "ClubNr updated") : NotOpen();

    public DbBridgeResult ChangeClubNrByStartNr(int newClubNr, int startNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeClubNrByStartNr(_ctx, newClubNr, startNr), "ClubNr updated") : NotOpen();

    public DbBridgeResult ChangeClubNrByChipNr(int dayNo, int newClubNr, int chipNr) =>
        IsOpen ? WrapWrite(dayNo, "ClubNr updated",
            () => DbBridgeNative.DbChangeClubNrByChipNr(_ctx, dayNo, newClubNr, chipNr)) : NotOpen();

    // ── CSV bulk read ────────────────────────────────────────────────────────
    // Uses 2-call buffer pattern: first call (null buffer) returns needed byte count;
    // second call with allocated buffer fills it and returns actual byte count.

    private (DbBridgeResult Result, string? Csv) ReadCsvBuffer(Func<IntPtr, int, int> dllCall)
    {
        if (!IsOpen) return (NotOpen(), null);

        // First call: IntPtr.Zero → DLL returns needed byte count (not including null terminator).
        int needed = dllCall(IntPtr.Zero, 0);
        _log?.Invoke($"CSV size query: {needed} bytes needed");
        if (needed <= 0) return (Fail(needed), null);

        // Second call: allocate len+1 bytes (DLL requires room for null terminator).
        IntPtr buf = Marshal.AllocHGlobal(needed + 1);
        try
        {
            Marshal.WriteByte(buf + needed, 0); // ensure null terminator
            int actual = dllCall(buf, needed + 1);
            _log?.Invoke($"CSV read: {actual} bytes actual");
            if (actual <= 0) return (Fail(actual), null);
            return (Ok("OK"), Marshal.PtrToStringAnsi(buf) ?? string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    public (DbBridgeResult Result, string? Csv) GetAllClasses() =>
        ReadCsvBuffer((buf, size) => DbBridgeNative.DbGetAllClasses(_ctx, buf, size));

    public (DbBridgeResult Result, string? Csv) GetAllClubs() =>
        ReadCsvBuffer((buf, size) => DbBridgeNative.DbGetAllClubs(_ctx, buf, size));

    public (DbBridgeResult Result, string? Csv) GetAllTeiln() =>
        ReadCsvBuffer((buf, size) => DbBridgeNative.DbGetAllTeiln(_ctx, buf, size));

    public (DbBridgeResult Result, string? Csv) GetAllTeilnDay(int dayNo) =>
        ReadCsvBuffer((buf, size) => DbBridgeNative.DbGetAllTeilnDay(_ctx, dayNo, buf, size));

    // ── Update Name ───────────────────────────────────────────────────────────

    /// <summary>Updates the Name field in the given table (e.g. "Teiln") by IdNr.</summary>
    public DbBridgeResult UpdateName(string tableName, int idNr, string newName) =>
        IsOpen ? Wrap(DbBridgeNative.DbUpdateNameRaw(_ctx, EncodeString(tableName), idNr, EncodeString(newName)), "Name updated") : NotOpen();

    // ── Buffer parsing helpers ────────────────────────────────────────────────

    /// <summary>
    /// Parses the raw buffer from GetIdNrListByStartNr into a list of IdNr integers.
    /// Handles comma, newline, space, semicolon, and pipe-separated values.
    /// </summary>
    public static List<int> ParseIdNrList(string? raw)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var token in raw.Split(new[] { ',', '\n', '\r', ' ', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(token.Trim(), out int id) && id > 0)
                result.Add(id);
        return result;
    }

    /// <summary>
    /// Parses the raw buffer from GetTeilnInfoByIdNr into a key→value dictionary.
    /// Tries line-by-line "Key=Value" format first. Returns empty dict if format not recognized.
    /// </summary>
    public static Dictionary<string, string> ParseTeilnInfo(string? raw)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return fields;
        foreach (var line in raw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = line.IndexOf('=');
            if (eq > 0)
                fields[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return fields;
    }
}
