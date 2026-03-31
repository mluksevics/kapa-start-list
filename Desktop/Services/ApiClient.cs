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
    public string? LastError { get; private set; }

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

        return await GetRunnersRawAsync(url, ct);
    }

    private async Task<GetRunnersResponse?> GetRunnersRawAsync(string url, CancellationToken ct)
    {
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
        bool touchAll = false,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/competitions/{date}/runners";
        if (touchAll)
            url += "?touchAll=true";
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

    public async Task<UpsertLookupResponse?> UpsertClassesAsync(
        UpsertLookupRequest request,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/lookups/classes";
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

    public async Task<LookupCountsResponse?> GetLookupCountsAsync(string date, CancellationToken ct = default)
    {
        LastError = null;
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/lookups/counts/{date}";
        try
        {
            var msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.Headers.Add("X-Api-Key", S.ApiKey);
            var response = await _http.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode)
            {
                LastError = await BuildHttpErrorAsync(response, ct);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<LookupCountsResponse>(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task<DeleteTodayDataResponse?> DeleteCompetitionDataAsync(string date, CancellationToken ct = default)
    {
        LastError = null;
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/competitions/{date}/data";
        try
        {
            var msg = new HttpRequestMessage(HttpMethod.Delete, url);
            msg.Headers.Add("X-Api-Key", S.ApiKey);
            var response = await _http.SendAsync(msg, ct);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DeleteTodayDataResponse>(ct);

            LastError = await BuildHttpErrorAsync(response, ct);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    private static async Task<string> BuildHttpErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.Length > 300)
            body = body[..300];
        return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}";
    }
}
