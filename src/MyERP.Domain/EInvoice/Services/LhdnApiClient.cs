using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MyERP.EInvoice.Services;

/// <summary>
/// HTTP client implementation for LHDN MyInvois API.
/// Migrated from myinvois taxpayerlogin.py, submit_purchase.py, cancel_doc.py, get_status.py.
/// </summary>
public class LhdnApiClient : ILhdnApiClient, ITransientDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LhdnApiClient> _logger;

    private const string SandboxBaseUrl = "https://preprod-api.myinvois.hasil.gov.my";
    private const string ProductionBaseUrl = "https://api.myinvois.hasil.gov.my";

    public LhdnApiClient(IHttpClientFactory httpClientFactory, ILogger<LhdnApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string GetBaseUrl(LhdnEnvironment environment) =>
        environment == LhdnEnvironment.Production ? ProductionBaseUrl : SandboxBaseUrl;

    public async Task<string> GetAccessTokenAsync(string clientId, string clientSecret, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/connect/token";

        var content = new FormUrlEncodedContent(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId),
            new System.Collections.Generic.KeyValuePair<string, string>("client_secret", clientSecret),
            new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "client_credentials"),
            new System.Collections.Generic.KeyValuePair<string, string>("scope", "InvoicingAPI"),
        });

        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    public async Task<LhdnSubmissionResponse> SubmitDocumentAsync(string accessToken, string xmlDocument, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/api/v1.0/documentsubmissions";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(xmlDocument, Encoding.UTF8, "application/xml");

        var response = await client.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LHDN submission failed: {StatusCode} {Body}", response.StatusCode, rawJson);
            return new LhdnSubmissionResponse { IsSuccess = false, ErrorMessage = rawJson, RawJson = rawJson };
        }

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var submissionUid = root.GetProperty("submissionUid").GetString();
        string? documentUuid = null;
        string? longId = null;

        if (root.TryGetProperty("acceptedDocuments", out var accepted) && accepted.GetArrayLength() > 0)
        {
            var first = accepted[0];
            documentUuid = first.GetProperty("uuid").GetString();
            if (first.TryGetProperty("longId", out var lid))
                longId = lid.GetString();
        }

        return new LhdnSubmissionResponse
        {
            IsSuccess = true,
            SubmissionUid = submissionUid,
            DocumentUuid = documentUuid,
            LongId = longId,
            RawJson = rawJson
        };
    }

    public async Task<LhdnStatusResponse> GetDocumentStatusAsync(string accessToken, string submissionUid, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/api/v1.0/documentsubmissions/{submissionUid}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var status = "Unknown";
        string? uuid = null;
        string? lid = null;

        if (root.TryGetProperty("documentSummary", out var summary) && summary.GetArrayLength() > 0)
        {
            var first = summary[0];
            status = first.GetProperty("status").GetString() ?? "Unknown";
            if (first.TryGetProperty("uuid", out var u)) uuid = u.GetString();
            if (first.TryGetProperty("longId", out var l)) lid = l.GetString();
        }

        return new LhdnStatusResponse { Status = status, DocumentUuid = uuid, LongId = lid, RawJson = rawJson };
    }

    public async Task<LhdnCancelResponse> CancelDocumentAsync(string accessToken, string documentUuid, string reason, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/api/v1.0/documents/state/{documentUuid}/state";

        var payload = JsonSerializer.Serialize(new { status = "cancelled", reason });
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        return new LhdnCancelResponse
        {
            IsSuccess = response.IsSuccessStatusCode,
            ErrorMessage = response.IsSuccessStatusCode ? null : rawJson,
            RawJson = rawJson
        };
    }

    public async Task<LhdnTaxpayerSearchResponse> SearchTaxpayerAsync(string accessToken, string idType, string idValue, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/api/v1.0/taxpayer/search/tin?idType={Uri.EscapeDataString(idType)}&idValue={Uri.EscapeDataString(idValue)}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new LhdnTaxpayerSearchResponse { IsFound = false, RawJson = rawJson };
        }

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        return new LhdnTaxpayerSearchResponse
        {
            IsFound = true,
            Tin = root.TryGetProperty("tin", out var tin) ? tin.GetString() : null,
            TaxpayerName = root.TryGetProperty("name", out var name) ? name.GetString() : null,
            RawJson = rawJson
        };
    }
}
