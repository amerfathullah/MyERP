using System.Threading.Tasks;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Interface for LHDN MyInvois API communication.
/// Migrated from myinvois taxpayerlogin.py / submit_purchase.py.
/// Isolated behind interface for testability and environment switching (Sandbox/Production).
/// </summary>
public interface ILhdnApiClient
{
    /// <summary>Get OAuth2 access token from LHDN using client credentials.</summary>
    Task<string> GetAccessTokenAsync(string clientId, string clientSecret, LhdnEnvironment environment);

    /// <summary>Submit a UBL 2.1 XML document to LHDN.</summary>
    Task<LhdnSubmissionResponse> SubmitDocumentAsync(string accessToken, string xmlDocument, LhdnEnvironment environment);

    /// <summary>Get the status of a previously submitted document.</summary>
    Task<LhdnStatusResponse> GetDocumentStatusAsync(string accessToken, string submissionUid, LhdnEnvironment environment);

    /// <summary>Cancel a submitted document within 72-hour window.</summary>
    Task<LhdnCancelResponse> CancelDocumentAsync(string accessToken, string documentUuid, string reason, LhdnEnvironment environment);

    /// <summary>Search for taxpayer TIN by ID type and value.</summary>
    Task<LhdnTaxpayerSearchResponse> SearchTaxpayerAsync(string accessToken, string idType, string idValue, LhdnEnvironment environment);
}

public class LhdnSubmissionResponse
{
    public bool IsSuccess { get; set; }
    public string? SubmissionUid { get; set; }
    public string? DocumentUuid { get; set; }
    public string? LongId { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? RawJson { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LhdnStatusResponse
{
    public string Status { get; set; } = null!; // "Valid", "Invalid", "Cancelled"
    public string? DocumentUuid { get; set; }
    public string? LongId { get; set; }
    public string? RawJson { get; set; }
}

public class LhdnCancelResponse
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawJson { get; set; }
}

public class LhdnTaxpayerSearchResponse
{
    public bool IsFound { get; set; }
    public string? Tin { get; set; }
    public string? TaxpayerName { get; set; }
    public string? RawJson { get; set; }
}
