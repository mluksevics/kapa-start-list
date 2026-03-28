namespace StartRef.Desktop.DbBridge;

/// <summary>Result returned by every DbBridgeService method.</summary>
public record DbBridgeResult(bool Success, int Code, string Message);
