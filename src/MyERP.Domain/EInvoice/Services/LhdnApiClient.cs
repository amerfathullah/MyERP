using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
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
    private const string SandboxPortalBaseUrl = "https://preprod.myinvois.hasil.gov.my";
    private const string ProductionPortalBaseUrl = "https://myinvois.hasil.gov.my";

    public LhdnApiClient(IHttpClientFactory httpClientFactory, ILogger<LhdnApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string GetBaseUrl(LhdnEnvironment environment) =>
        environment == LhdnEnvironment.Production ? ProductionBaseUrl : SandboxBaseUrl;

    /// <summary>
    /// Builds the official LHDN MyInvois QR verification URL when both DocumentUuid and LongId are available.
    /// Mirrors myinvois createxml.py lines 1279-1283.
    /// </summary>
    public static string? BuildQrVerificationUrl(LhdnEnvironment environment, string? documentUuid, string? longId)
    {
        if (string.IsNullOrWhiteSpace(documentUuid) || string.IsNullOrWhiteSpace(longId))
            return null;

        var portalBase = environment == LhdnEnvironment.Production ? ProductionPortalBaseUrl : SandboxPortalBaseUrl;
        return $"{portalBase}/{documentUuid}/share/{longId}";
    }

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

    /// <summary>
    /// Builds the LHDN MyInvois <c>/api/v1.0/documentsubmissions</c> JSON envelope wrapping Base64 XML,
    /// lowercase SHA-256 hex digest, and invoice code number per myinvois <c>original.py</c> lines 377-395.
    /// </summary>
    public static string BuildSubmissionPayloadJson(string xmlDocument, string? codeNumber = null)
    {
        var xmlBytes = Encoding.UTF8.GetBytes(xmlDocument ?? string.Empty);
        var sha256Hex = Convert.ToHexString(SHA256.HashData(xmlBytes)).ToLowerInvariant();
        var encodedXml = Convert.ToBase64String(xmlBytes);
        var resolvedCodeNumber = !string.IsNullOrWhiteSpace(codeNumber)
            ? codeNumber.Trim()
            : (TryExtractInvoiceCodeNumber(xmlDocument) ?? sha256Hex[..16]);

        return JsonSerializer.Serialize(new
        {
            documents = new[]
            {
                new
                {
                    format = "XML",
                    documentHash = sha256Hex,
                    codeNumber = resolvedCodeNumber,
                    document = encodedXml
                }
            }
        });
    }

    private static string? TryExtractInvoiceCodeNumber(string? xmlDocument)
    {
        if (string.IsNullOrWhiteSpace(xmlDocument))
        {
            return null;
        }

        try
        {
            var root = XDocument.Parse(xmlDocument).Root;
            var idElem = root?.Elements().FirstOrDefault(e => e.Name.LocalName == "ID");
            var val = idElem?.Value?.Trim();
            return string.IsNullOrWhiteSpace(val) ? null : val;
        }
        catch
        {
            return null;
        }
    }

    public async Task<LhdnSubmissionResponse> SubmitDocumentAsync(string accessToken, string xmlDocument, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/api/v1.0/documentsubmissions";

        var payloadJson = BuildSubmissionPayloadJson(xmlDocument);
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LHDN submission failed: {StatusCode} {Body}", response.StatusCode, rawJson);
            return new LhdnSubmissionResponse { IsSuccess = false, StatusCode = statusCode, ErrorMessage = rawJson, RawJson = rawJson };
        }

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        string? submissionUid = root.TryGetProperty("submissionUid", out var suidProp) ? suidProp.GetString() : null;
        string? documentUuid = null;
        string? longId = null;

        if (root.TryGetProperty("acceptedDocuments", out var accepted) && accepted.GetArrayLength() > 0)
        {
            var first = accepted[0];
            if (first.TryGetProperty("uuid", out var u))
                documentUuid = u.GetString();
            if (first.TryGetProperty("longId", out var lid))
                longId = lid.GetString();
        }

        // LHDN returns HTTP 202 even when documents are rejected in rejectedDocuments[]
        if (string.IsNullOrWhiteSpace(documentUuid))
        {
            string? rejectionReason = null;
            if (root.TryGetProperty("rejectedDocuments", out var rejected) && rejected.GetArrayLength() > 0)
            {
                var firstRejected = rejected[0];
                if (firstRejected.TryGetProperty("error", out var errProp))
                {
                    rejectionReason = errProp.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : errProp.GetRawText();
                }
                else
                {
                    rejectionReason = firstRejected.GetRawText();
                }
            }

            _logger.LogWarning("LHDN submission returned no accepted document UUID: {Body}", rawJson);
            return new LhdnSubmissionResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                SubmissionUid = submissionUid,
                ErrorMessage = rejectionReason ?? "LHDN rejected document (no accepted document UUID).",
                RawJson = rawJson
            };
        }

        return new LhdnSubmissionResponse
        {
            IsSuccess = true,
            StatusCode = statusCode,
            SubmissionUid = submissionUid,
            DocumentUuid = documentUuid,
            LongId = longId,
            QrCodeUrl = BuildQrVerificationUrl(environment, documentUuid, longId),
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
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LHDN status query failed: {StatusCode} {Body}", response.StatusCode, rawJson);
            return new LhdnStatusResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Status = "Unknown",
                ErrorMessage = rawJson,
                RawJson = rawJson
            };
        }

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var status = "Submitted";
        string? uuid = null;
        string? lid = null;

        if (root.TryGetProperty("documentSummary", out var summary) && summary.GetArrayLength() > 0)
        {
            var first = summary[0];
            if (first.TryGetProperty("status", out var st) && !string.IsNullOrWhiteSpace(st.GetString()))
                status = st.GetString()!;
            if (first.TryGetProperty("uuid", out var u)) uuid = u.GetString();
            if (first.TryGetProperty("longId", out var l)) lid = l.GetString();
        }
        else if (root.TryGetProperty("overallStatus", out var overall) && !string.IsNullOrWhiteSpace(overall.GetString()))
        {
            // Per myinvois original.py:665-672 — when documentSummary is empty right after submission,
            // fall back to overallStatus if present, otherwise "Submitted" (never "Unknown").
            status = overall.GetString()!;
        }

        return new LhdnStatusResponse
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Status = status,
            DocumentUuid = uuid,
            LongId = lid,
            QrCodeUrl = BuildQrVerificationUrl(environment, uuid, lid),
            RawJson = rawJson
        };
    }

    public async Task<LhdnCancelResponse> CancelDocumentAsync(string accessToken, string documentUuid, string reason, LhdnEnvironment environment)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        var url = $"{GetBaseUrl(environment)}/api/v1.0/documents/state/{documentUuid}/state";

        // Per myinvois cancel_doc.py:94 — default reason when blank
        var effectiveReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled from ERP system by user" : reason;
        var payload = JsonSerializer.Serialize(new { status = "cancelled", reason = effectiveReason });
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var rawJson = await response.Content.ReadAsStringAsync();

        return new LhdnCancelResponse
        {
            IsSuccess = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            ErrorMessage = response.IsSuccessStatusCode ? null : rawJson,
            RawJson = rawJson
        };
    }

    public async Task<LhdnTaxpayerSearchResponse> SearchTaxpayerAsync(
        string accessToken, string? idType, string? idValue, LhdnEnvironment environment, string? taxpayerName = null)
    {
        var client = _httpClientFactory.CreateClient("LhdnApi");
        string url;
        if (!string.IsNullOrWhiteSpace(idType) && !string.IsNullOrWhiteSpace(idValue))
        {
            url = $"{GetBaseUrl(environment)}/api/v1.0/taxpayer/search/tin?idType={Uri.EscapeDataString(idType)}&idValue={Uri.EscapeDataString(idValue)}";
        }
        else if (!string.IsNullOrWhiteSpace(taxpayerName))
        {
            url = $"{GetBaseUrl(environment)}/api/v1.0/taxpayer/search/tin?taxpayerName={Uri.EscapeDataString(taxpayerName)}";
        }
        else
        {
            return new LhdnTaxpayerSearchResponse
            {
                IsFound = false,
                StatusCode = 400,
                ErrorMessage = "As per LHDN Regulations, either ID Type and Value or Taxpayer Name must be present."
            };
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await client.SendAsync(request);
            var rawJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var msg = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Taxpayer not found in LHDN database"
                    : (!string.IsNullOrWhiteSpace(idType)
                        ? $"Status code: {(int)response.StatusCode} As per LHDN, either idType or idValue is wrong"
                        : $"Status code: {(int)response.StatusCode} As per LHDN, either type or value or taxpayer data is wrong");
                _logger.LogWarning("LHDN SearchTaxpayer failed: {StatusCode} {Body}", response.StatusCode, rawJson);
                return new LhdnTaxpayerSearchResponse { IsFound = false, StatusCode = (int)response.StatusCode, RawJson = rawJson, ErrorMessage = msg };
            }

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // Per Gotcha #3957 / myinvois search_taxpayer.py:78 — data.get("tin") or data.get("data", {}).get("tin")
            string? tinVal = null;
            if (root.TryGetProperty("tin", out var tinProp) && tinProp.ValueKind == JsonValueKind.String)
                tinVal = tinProp.GetString();
            if (string.IsNullOrWhiteSpace(tinVal)
                && root.TryGetProperty("data", out var dataProp)
                && dataProp.ValueKind == JsonValueKind.Object
                && dataProp.TryGetProperty("tin", out var dataTinProp)
                && dataTinProp.ValueKind == JsonValueKind.String)
                tinVal = dataTinProp.GetString();

            string? nameVal = null;
            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                nameVal = nameProp.GetString();
            if (string.IsNullOrWhiteSpace(nameVal)
                && root.TryGetProperty("data", out var dataNameProp)
                && dataNameProp.ValueKind == JsonValueKind.Object
                && dataNameProp.TryGetProperty("name", out var dataNameVal)
                && dataNameVal.ValueKind == JsonValueKind.String)
                nameVal = dataNameVal.GetString();

            if (string.IsNullOrWhiteSpace(tinVal))
            {
                return new LhdnTaxpayerSearchResponse
                {
                    IsFound = false,
                    StatusCode = (int)response.StatusCode,
                    Tin = null,
                    TaxpayerName = nameVal,
                    RawJson = rawJson,
                    ErrorMessage = "TIN not found in API response."
                };
            }

            return new LhdnTaxpayerSearchResponse
            {
                IsFound = true,
                StatusCode = (int)response.StatusCode,
                Tin = tinVal,
                TaxpayerName = nameVal,
                RawJson = rawJson
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LHDN SearchTaxpayer exception");
            return new LhdnTaxpayerSearchResponse
            {
                IsFound = false,
                StatusCode = 500,
                ErrorMessage = $"API request failed: {ex.Message}"
            };
        }
    }
}
