using System.Text;

namespace StartRef.Desktop;

/// <summary>Result returned by every DbBridgeService method.</summary>
public record DbBridgeResult(bool Success, int Code, string Message);

/// <summary>Parsed etap (stage) info from DbGetEtapInfo.</summary>
public record EtapInfo(string Name, string Date, int Nullzeit)
{
    /// <summary>Nullzeit converted from DLL internal units (seconds * 100) to hh:mm:ss.</summary>
    public string NullzeitFormatted
    {
        get
        {
            try { return TimeSpan.FromSeconds(Nullzeit / 100.0).ToString(@"hh\:mm\:ss"); }
            catch { return Nullzeit.ToString(); }
        }
    }
}

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

    /// <summary>True if DbBridge.dll is present next to the EXE.</summary>
    public static bool IsAvailable =>
        File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbBridge.dll"));

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
            _ctx = DbBridgeNative.DbOpenRaw(EncodeString(dataDir));
            if (_ctx == IntPtr.Zero)
            {
                _log?.Invoke("DbOpen returned NULL — check path and DLL dependencies.");
                _log?.Invoke($"Check DLL logs: {GlobalDllLogPath}");
                _log?.Invoke($"Check DB log: {DbErrorLogPath(_dataDir)}");
                return false;
            }
            _log?.Invoke($"DB opened: {dataDir}");
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
        var buffer = new byte[1024];
        try { DbBridgeNative.DbGetLastError(_ctx, buffer, buffer.Length); }
        catch { /* ignore */ }
        return DecodeBuffer(buffer);
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

    public (DbBridgeResult Result, EtapInfo? Info) GetEtapInfo(int dayNo)
    {
        if (!IsOpen) return (NotOpen(), null);
        var nameBuf = new byte[256];
        var dateBuf = new byte[256];
        int code = DbBridgeNative.DbGetEtapInfo(_ctx, dayNo, nameBuf, 255, dateBuf, 255, out int nullzeit);
        var result = Wrap(code, "OK");
        if (!result.Success) return (result, null);
        return (result, new EtapInfo(DecodeBuffer(nameBuf), DecodeBuffer(dateBuf), nullzeit));
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
        var buf = new byte[4096];
        int code = DbBridgeNative.DbGetTeilnInfoByIdNr(_ctx, idNr, buf, buf.Length);
        var result = Wrap(code, "OK");
        return (result, result.Success ? DecodeBuffer(buf) : null);
    }

    public (DbBridgeResult Result, string? Raw) GetIdNrListByStartNr(int startNr)
    {
        if (!IsOpen) return (NotOpen(), null);
        var buf = new byte[4096];
        int code = DbBridgeNative.DbGetIdNrListByStartNr(_ctx, startNr, buf, buf.Length);
        var result = Wrap(code, "OK");
        return (result, result.Success ? DecodeBuffer(buf) : null);
    }

    public (DbBridgeResult Result, string? Raw) GetIdNrListByChipNr(int dayNo, int chipNr)
    {
        if (!IsOpen) return (NotOpen(), null);
        var buf = new byte[4096];
        int code = DbBridgeNative.DbGetIdNrListByChipNr(_ctx, dayNo, chipNr, buf, buf.Length);
        var result = Wrap(code, "OK");
        return (result, result.Success ? DecodeBuffer(buf) : null);
    }

    // ── Change StartTime ─────────────────────────────────────────────────────

    public DbBridgeResult ChangeStartTimeByIdNr(int dayNo, string hhmmss, int idNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeStartTimeByIdNr(_ctx, dayNo, hhmmss, idNr), "StartTime updated") : NotOpen();

    public DbBridgeResult ChangeStartTimeByStartNr(int dayNo, string hhmmss, int startNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeStartTimeByStartNr(_ctx, dayNo, hhmmss, startNr), "StartTime updated") : NotOpen();

    public DbBridgeResult ChangeStartTimeByChipNr(int dayNo, string hhmmss, int chipNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeStartTimeByChipNr(_ctx, dayNo, hhmmss, chipNr), "StartTime updated") : NotOpen();

    // ── Change ChipNr ────────────────────────────────────────────────────────

    public DbBridgeResult ChangeChipNrByIdNr(int dayNo, int newChipNr, int idNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeChipNrByIdNr(_ctx, dayNo, newChipNr, idNr), "ChipNr updated") : NotOpen();

    public DbBridgeResult ChangeChipNrByStartNr(int dayNo, int newChipNr, int startNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeChipNrByStartNr(_ctx, dayNo, newChipNr, startNr), "ChipNr updated") : NotOpen();

    public DbBridgeResult ChangeChipNrByOldChipNr(int dayNo, int newChipNr, int oldChipNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeChipNrByOldChipNr(_ctx, dayNo, newChipNr, oldChipNr), "ChipNr updated") : NotOpen();

    // ── Change KatNr ─────────────────────────────────────────────────────────

    public DbBridgeResult ChangeKatNrByIdNr(int newKatNr, int idNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeKatNrByIdNr(_ctx, newKatNr, idNr), "KatNr updated") : NotOpen();

    public DbBridgeResult ChangeKatNrByStartNr(int newKatNr, int startNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeKatNrByStartNr(_ctx, newKatNr, startNr), "KatNr updated") : NotOpen();

    public DbBridgeResult ChangeKatNrByChipNr(int dayNo, int newKatNr, int chipNr) =>
        IsOpen ? Wrap(DbBridgeNative.DbChangeKatNrByChipNr(_ctx, dayNo, newKatNr, chipNr), "KatNr updated") : NotOpen();

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
