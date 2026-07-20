using System;
using System.Threading.Tasks;
using MyERP.EInvoice.Services;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace MyERP.EInvoice;

/// <summary>
/// Manages LHDN MyInvois e-Invoice connection settings and authentication.
/// Handles OAuth2 client credentials flow for obtaining API access tokens.
/// </summary>
[Authorize(MyERPPermissions.EInvoice.Default)]
public class EInvoiceSettingsAppService : ApplicationService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly ILhdnApiClient _lhdnApiClient;

    public EInvoiceSettingsAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        ILhdnApiClient lhdnApiClient)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _lhdnApiClient = lhdnApiClient;
    }

    /// <summary>
    /// Get current e-Invoice connection status and configuration.
    /// Does NOT return secrets (client_secret, certificate password).
    /// </summary>
    public async Task<EInvoiceConnectionStatusDto> GetConnectionStatusAsync()
    {
        var clientId = await _settingProvider.GetOrNullAsync("EInvoice.ClientId");
        var environment = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken");
        var tokenExpiry = await _settingProvider.GetOrNullAsync("EInvoice.TokenExpiresAt");
        var pfxConfigured = await _settingProvider.GetOrNullAsync("EInvoice.CertificatePfxBase64");

        var isConnected = !string.IsNullOrEmpty(accessToken);
        DateTime? expiresAt = null;
        if (DateTime.TryParse(tokenExpiry, out var parsed))
            expiresAt = parsed;

        var isTokenExpired = expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow;

        return new EInvoiceConnectionStatusDto
        {
            IsConfigured = !string.IsNullOrEmpty(clientId),
            IsConnected = isConnected && !isTokenExpired,
            IsTokenExpired = isTokenExpired,
            Environment = environment,
            ClientId = clientId,
            TokenExpiresAt = expiresAt,
            IsCertificateConfigured = !string.IsNullOrEmpty(pfxConfigured),
        };
    }

    /// <summary>
    /// Save LHDN API credentials. Does NOT authenticate — call ConnectAsync after saving.
    /// </summary>
    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task SaveCredentialsAsync(SaveEInvoiceCredentialsDto input)
    {
        await _settingManager.SetGlobalAsync("EInvoice.ClientId", input.ClientId);
        await _settingManager.SetGlobalAsync("EInvoice.Environment", input.Environment);

        if (!string.IsNullOrEmpty(input.ClientSecret))
        {
            await _settingManager.SetGlobalAsync("EInvoice.ClientSecret", input.ClientSecret);
        }

        Logger.LogInformation("LHDN e-Invoice credentials saved for environment: {Env}", input.Environment);
    }

    /// <summary>
    /// Authenticate with LHDN using stored credentials and obtain access token.
    /// Token is stored in settings for use by submission/status/cancel endpoints.
    /// </summary>
    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task<EInvoiceConnectResultDto> ConnectAsync()
    {
        var clientId = await _settingProvider.GetOrNullAsync("EInvoice.ClientId");
        var clientSecret = await _settingProvider.GetOrNullAsync("EInvoice.ClientSecret");
        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return new EInvoiceConnectResultDto
            {
                IsSuccess = false,
                ErrorMessage = "LHDN credentials not configured. Please save Client ID and Secret first.",
            };
        }

        var environment = Enum.Parse<LhdnEnvironment>(envString);

        try
        {
            var accessToken = await _lhdnApiClient.GetAccessTokenAsync(clientId, clientSecret, environment);

            // LHDN tokens typically expire in 1 hour
            var expiresAt = DateTime.UtcNow.AddMinutes(55); // 5-minute safety buffer

            await _settingManager.SetGlobalAsync("EInvoice.AccessToken", accessToken);
            await _settingManager.SetGlobalAsync("EInvoice.TokenExpiresAt", expiresAt.ToString("O"));

            Logger.LogInformation("LHDN authentication successful. Token expires at {ExpiresAt}", expiresAt);

            return new EInvoiceConnectResultDto
            {
                IsSuccess = true,
                TokenExpiresAt = expiresAt,
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "LHDN authentication failed");
            return new EInvoiceConnectResultDto
            {
                IsSuccess = false,
                ErrorMessage = $"Authentication failed: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Upload digital signing certificate (PFX/P12) for XAdES document signing.
    /// </summary>
    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task SaveCertificateAsync(SaveEInvoiceCertificateDto input)
    {
        await _settingManager.SetGlobalAsync("EInvoice.CertificatePfxBase64", input.CertificateBase64);

        if (!string.IsNullOrEmpty(input.CertificatePassword))
        {
            await _settingManager.SetGlobalAsync("EInvoice.CertificatePassword", input.CertificatePassword);
        }

        Logger.LogInformation("LHDN digital signing certificate saved");
    }

    /// <summary>
    /// Search for taxpayer TIN using ID type and value.
    /// Useful for validating customer/supplier TIN before invoice submission.
    /// </summary>
    public async Task<TaxpayerSearchResultDto> SearchTaxpayerAsync(string idType, string idValue)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken");
        if (string.IsNullOrEmpty(accessToken))
        {
            return new TaxpayerSearchResultDto
            {
                IsSuccess = false,
                ErrorMessage = "Not connected to LHDN. Please authenticate first.",
            };
        }

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        try
        {
            var response = await _lhdnApiClient.SearchTaxpayerAsync(accessToken, idType, idValue, environment);
            return new TaxpayerSearchResultDto
            {
                IsSuccess = response.IsFound,
                Tin = response.Tin,
                Name = response.TaxpayerName,
                IdType = idType,
                IdValue = idValue,
                ErrorMessage = response.IsFound ? null : "Taxpayer not found",
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "LHDN taxpayer search failed for {IdType}:{IdValue}", idType, idValue);
            return new TaxpayerSearchResultDto
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
            };
        }
    }
}
