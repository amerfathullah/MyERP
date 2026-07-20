using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.EInvoice;

/// <summary>Current LHDN connection status (read-only, no secrets exposed).</summary>
public class EInvoiceConnectionStatusDto
{
    /// <summary>Whether client credentials are configured.</summary>
    public bool IsConfigured { get; set; }

    /// <summary>Whether an active (non-expired) access token exists.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Whether the token has expired (requires re-authentication).</summary>
    public bool IsTokenExpired { get; set; }

    /// <summary>Current environment (Sandbox or Production).</summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>Client ID (not secret — safe to expose).</summary>
    public string? ClientId { get; set; }

    /// <summary>When the current access token expires.</summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>Whether a signing certificate is configured.</summary>
    public bool IsCertificateConfigured { get; set; }
}

/// <summary>Input for saving LHDN API credentials.</summary>
public class SaveEInvoiceCredentialsDto
{
    [Required][StringLength(200)]
    public string ClientId { get; set; } = null!;

    /// <summary>Only sent when changing the secret. Null = keep existing.</summary>
    [StringLength(500)]
    public string? ClientSecret { get; set; }

    [Required][StringLength(20)]
    public string Environment { get; set; } = "Sandbox";
}

/// <summary>Result of LHDN authentication attempt.</summary>
public class EInvoiceConnectResultDto
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}

/// <summary>Input for saving the digital signing certificate.</summary>
public class SaveEInvoiceCertificateDto
{
    /// <summary>PFX/P12 certificate as Base64 string.</summary>
    [Required]
    public string CertificateBase64 { get; set; } = null!;

    /// <summary>Certificate password. Null = keep existing.</summary>
    [StringLength(200)]
    public string? CertificatePassword { get; set; }
}

/// <summary>Result of LHDN taxpayer TIN search.</summary>
public class TaxpayerSearchResultDto
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Tin { get; set; }
    public string? Name { get; set; }
    public string? IdType { get; set; }
    public string? IdValue { get; set; }
}
