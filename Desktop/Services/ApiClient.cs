using System.Net.Http.Json;
using StartRef.Desktop.Models;

namespace StartRef.Desktop.Services;

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
            var msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.Headers.Add("X-Api-Key", S.ApiKey);
            var response = await _http.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<GetRunnersResponse>(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UpsertLookupResponse?> UpsertClubsAsync(
        UpsertLookupRequest request,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/lookups/clubs";
        var msg = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Add("X-Api-Key", S.ApiKey);

        try
        {
            var response = await _http.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UpsertLookupResponse>(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<LookupCountsResponse?> GetLookupCountsAsync(CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/lookups/counts";
        try
        {
            var msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.Headers.Add("X-Api-Key", S.ApiKey);
            var response = await _http.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LookupCountsResponse>(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
