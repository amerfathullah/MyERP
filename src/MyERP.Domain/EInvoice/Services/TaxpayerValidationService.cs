using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Validates Taxpayer Information (TIN/BRN) using the LHDN MyInvois API.
/// Migrated from myinvois search_taxpayer.py.
/// </summary>
public class TaxpayerValidationService : ITransientDependency
{
    private readonly ILhdnApiClient _lhdnApiClient;
    private readonly ISettingProvider _settingProvider;

    public TaxpayerValidationService(ILhdnApiClient lhdnApiClient, ISettingProvider settingProvider)
    {
        _lhdnApiClient = lhdnApiClient;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Validates a taxpayer's TIN against the LHDN database.
    /// Uses ID Type (e.g. BRN, NRIC, PASSPORT, ARMY) and ID Value, or Taxpayer Name.
    /// </summary>
    public async Task<LhdnTaxpayerSearchResponse> ValidateTaxpayerAsync(string? idType, string? idValue, string? taxpayerName = null)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Volo.Abp.UserFriendlyException("Cannot validate taxpayer. E-Invoice API is not connected or token is missing.");
        }

        var environment = ParseEnvironment(await _settingProvider.GetOrNullAsync("EInvoice.Environment"));

        var result = await _lhdnApiClient.SearchTaxpayerAsync(accessToken, idType, idValue, environment, taxpayerName);
        if (!result.IsFound && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            throw new Volo.Abp.UserFriendlyException($"API request failed: {result.ErrorMessage}");
        }

        return result;
    }

    public static LhdnEnvironment ParseEnvironment(string? envString) =>
        Enum.TryParse<LhdnEnvironment>(envString, ignoreCase: true, out var parsed)
            ? parsed
            : LhdnEnvironment.Sandbox;

    /// <summary>
    /// Formats LHDN taxpayer search failure message for customer/supplier TIN validation
    /// per MyInvois commit 7552df2 (<c>search_taxpayer.py: search_purchase_tin</c>).
    /// </summary>
    public static string FormatTaxpayerSearchFailureMessage(int statusCode, string? responseText)
    {
        var msg = !string.IsNullOrWhiteSpace(responseText)
            ? $"Status code: {statusCode} {responseText.Trim()}"
            : $"Status code: {statusCode} As per LHDN,either type or value or taxpayer data is wrong";
        return $"API request failed: {msg}";
    }
}
