using System.Net.Http.Json;
using Polly;
using Polly.Retry;
using StartRef.Desktop.Models;

namespace StartRef.Desktop.Services;

/// <summary>
/// HTTP client for the StartRef API.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<string>? _log;
    public string? LastError { get; private set; }

    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

    public ApiClient(Func<AppSettings> getSettings, Action<string>? log = null)
    {
        _getSettings = getSettings;
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .HandleResult(r => (int)r.StatusCode >= 500),
                OnRetry = args =>
                {
                    _log?.Invoke($"{DateTime.Now:HH:mm:ss} API retry {args.AttemptNumber + 1}/3 after {args.RetryDelay.TotalSeconds:F0}s — {args.Outcome.Exception?.Message ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}"}");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
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

    /// <summary>PUT /api/competitions/{date}/runners — bulk upload with Polly retry</summary>
    public async Task<BulkUploadResponse?> BulkUploadAsync(
        string date,
        BulkUploadRequest request,
        bool touchAll = false,
        CancellationToken ct = default)
    {
        LastError = null;
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/competitions/{date}/runners";
        if (touchAll)
            url += "?touchAll=true";

        try
        {
            var response = await _retryPipeline.ExecuteAsync(async token =>
            {
                var msg = new HttpRequestMessage(HttpMethod.Put, url)
                {
                    Content = JsonContent.Create(request)
                };
                msg.Headers.Add("X-Api-Key", S.ApiKey);
                return await _http.SendAsync(msg, token);
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                LastError = await BuildHttpErrorAsync(response, ct);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<BulkUploadResponse>(ct);
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

    public async Task<UpsertLookupResponse?> UpsertClassesAsync(
        string date,
        UpsertLookupRequest request,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/lookups/{date}/classes";
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
        string date,
        UpsertLookupRequest request,
        CancellationToken ct = default)
    {
        var url = $"{S.ApiBaseUrl.TrimEnd('/')}/api/lookups/{date}/clubs";
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
