namespace StartRef.Desktop.DbBridge;

/// <summary>Parsed etap (stage) info from DbGetEtapInfo.</summary>
public record DbEtapInfo(string Name, string Date, int Nullzeit)
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
