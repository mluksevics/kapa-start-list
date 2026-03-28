using System.Net.Http.Json;
using System.Text.Json;

namespace StartRef.Desktop;

/// <summary>
/// HTTP client for the StartRef API.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _getSettings;

    public ApiClient(Func<AppSettings> getSettings)
    {
        _getSettings = getSettings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private AppSettings S => _getSettings();

    /// <summary>GET /api/competitions/{date}/runners[?changedSince=ISO]</summary>
    public async Task<GetRunnersResponse?> GetRunnersAsync(
        string date,
        DateTimeOffset? changedSince = null,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/competitions/{date}/runners";
        if (changedSince.HasValue)
            url += $"?changedSince={Uri.EscapeDataString(changedSince.Value.ToString("O"))}";

        try
        {
            return await _http.GetFromJsonAsync<GetRunnersResponse>(url, ct);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>PUT /api/competitions/{date}/runners — bulk upload</summary>
    public async Task<BulkUploadResponse?> BulkUploadAsync(
        string date,
        BulkUploadRequest request,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/competitions/{date}/runners";
        var msg = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Add("X-Api-Key", S.ApiKey);

        try
        {
            var response = await _http.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<BulkUploadResponse>(ct);
        }
        catch
        {
            return null;
        }
    }
}
