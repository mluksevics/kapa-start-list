using System.Runtime.InteropServices;
using System.Text;

namespace StartRef.Desktop;

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

    // ── Test mode ────────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbSetTestMode(IntPtr ctxHandle, int dayNo);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int DbDisableTestMode(IntPtr ctxHandle);

    // ── Read ─────────────────────────────────────────────────────────────────

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbGetTeilnInfoByIdNr(IntPtr ctxHandle, int idNr, StringBuilder buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbGetIdNrListByStartNr(IntPtr ctxHandle, int startNr, StringBuilder buffer, int bufferSize);

    [DllImport("DbBridge.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int DbGetIdNrListByChipNr(IntPtr ctxHandle, int dayNo, int chipNr, StringBuilder buffer, int bufferSize);

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
}
