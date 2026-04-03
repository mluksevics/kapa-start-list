using System.Runtime.InteropServices;
using System.Text;

namespace StartRef.Desktop.DbBridge;

/// <summary>
/// Raw P/Invoke declarations for DbBridge.dll (32-bit DBISAM access bridge).
/// Process must run as x86. DLL must be placed next to the EXE.
/// </summary>
internal static class DbBridgeNative
{
    public const int DBR_OK = 1;
    public const int DBR_ERROR = 0;
    public const int DBR_NOT_FOUND = -1;
    public const int DBR_INVALID_DAY = -2;
    public const int DBR_DAY_NOT_ALLOWED = -3;
    public const int DBR_INVALID_TIME = -4;
    public const int DBR_CTX_NIL = -5;
    public const int DBR_MULTIPLE_MATCHES = -6;

    // ── Open / Close ─────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern IntPtr DbOpen(string dataDir);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern void DbClose(IntPtr ctxHandle);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbGetLastError(IntPtr ctxHandle, StringBuilder buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbGetLastError")]
    public static extern int DbGetLastErrorRaw(IntPtr ctxHandle, byte[] buffer, int bufferSize);

    // ── Etap / Config ────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbGetEtapInfo(
        IntPtr ctxHandle,
        int dayNo,
        StringBuilder nameBuf,
        int nameSize,
        StringBuilder dateBuf,
        int dateSize,
        out int nullzeit);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbGetEtapInfo")]
    public static extern int DbGetEtapInfoRaw(
        IntPtr ctxHandle,
        int dayNo,
        byte[] nameBuf,
        int nameSize,
        byte[] dateBuf,
        int dateSize,
        out int nullzeit);

    // ── Test mode ────────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbSetTestMode(IntPtr ctxHandle, int dayNo);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbDisableTestMode(IntPtr ctxHandle);

    // ── Read ─────────────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetTeilnInfoByIdNr(IntPtr ctxHandle, int idNr, StringBuilder buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbGetTeilnInfoByIdNr")]
    public static extern int DbGetTeilnInfoByIdNrRaw(IntPtr ctxHandle, int idNr, byte[] buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetIdNrListByStartNr(IntPtr ctxHandle, int startNr, StringBuilder buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbGetIdNrListByStartNr")]
    public static extern int DbGetIdNrListByStartNrRaw(IntPtr ctxHandle, int startNr, byte[] buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetIdNrListByChipNr(IntPtr ctxHandle, int dayNo, int chipNr, StringBuilder buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbGetIdNrListByChipNr")]
    public static extern int DbGetIdNrListByChipNrRaw(IntPtr ctxHandle, int dayNo, int chipNr, byte[] buffer, int bufferSize);

    // ── Change StartTime ─────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbChangeStartTimeByIdNr(IntPtr ctxHandle, int dayNo, string hhmmss, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbChangeStartTimeByStartNr(IntPtr ctxHandle, int dayNo, string hhmmss, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbChangeStartTimeByChipNr(IntPtr ctxHandle, int dayNo, string hhmmss, int chipNr);

    // ── Change ChipNr ────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeChipNrByIdNr(IntPtr ctxHandle, int dayNo, int newChipNr, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeChipNrByStartNr(IntPtr ctxHandle, int dayNo, int newChipNr, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeChipNrByOldChipNr(IntPtr ctxHandle, int dayNo, int newChipNr, int chipNr);

    // ── Change KatNr ─────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeKatNrByIdNr(IntPtr ctxHandle, int newKatNr, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeKatNrByStartNr(IntPtr ctxHandle, int newKatNr, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeKatNrByChipNr(IntPtr ctxHandle, int dayNo, int newKatNr, int chipNr);

    // ── Update Name (generic, any table) ────────────────────────────────────

    /// <summary>Updates the Name field of a record in the given table by IdNr (e.g. tableName="Teiln").</summary>
    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbUpdateName")]
    public static extern int DbUpdateNameRaw(IntPtr ctxHandle, byte[] tableName, int idNr, byte[] newName);

    // ── Change Name / Vorname ────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbChangeNameByIdNr")]
    public static extern int DbChangeNameByIdNrRaw(IntPtr ctxHandle, byte[] newName, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbChangeNameByStartNr")]
    public static extern int DbChangeNameByStartNrRaw(IntPtr ctxHandle, byte[] newName, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbChangeVornameByIdNr")]
    public static extern int DbChangeVornameByIdNrRaw(IntPtr ctxHandle, byte[] newVorname, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbChangeVornameByStartNr")]
    public static extern int DbChangeVornameByStartNrRaw(IntPtr ctxHandle, byte[] newVorname, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbChangeNameVornameByIdNr")]
    public static extern int DbChangeNameVornameByIdNrRaw(IntPtr ctxHandle, byte[] newName, byte[] newVorname, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "DbChangeNameVornameByStartNr")]
    public static extern int DbChangeNameVornameByStartNrRaw(IntPtr ctxHandle, byte[] newName, byte[] newVorname, int startNr);

    // ── DNS (NCKen) ──────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbSetDNSByIdNr(IntPtr ctxHandle, int dayNo, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbClearDNSByIdNr(IntPtr ctxHandle, int dayNo, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbSetDNSByStartNr(IntPtr ctxHandle, int dayNo, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbClearDNSByStartNr(IntPtr ctxHandle, int dayNo, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbSetDNSByChipNr(IntPtr ctxHandle, int dayNo, int chipNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbClearDNSByChipNr(IntPtr ctxHandle, int dayNo, int chipNr);

    // ── Change ClubNr ────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeClubNrByIdNr(IntPtr ctxHandle, int newClubNr, int idNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeClubNrByStartNr(IntPtr ctxHandle, int newClubNr, int startNr);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbChangeClubNrByChipNr(IntPtr ctxHandle, int dayNo, int newClubNr, int chipNr);

    // ── CSV bulk read (2-call buffer pattern, IntPtr for unmanaged alloc) ───

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetAllClasses(IntPtr ctxHandle, IntPtr buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetAllClubs(IntPtr ctxHandle, IntPtr buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetAllTeiln(IntPtr ctxHandle, IntPtr buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbGetAllTeilnDay(IntPtr ctxHandle, int dayNo, IntPtr buffer, int bufferSize);
}
