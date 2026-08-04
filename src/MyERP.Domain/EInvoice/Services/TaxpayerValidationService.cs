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
    /// Uses the ID Type (e.g. BRN, NRIC, PASSPORT, ARMY) and ID Value.
    /// </summary>
    public async Task<LhdnTaxpayerSearchResponse> ValidateTaxpayerAsync(string idType, string idValue)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Volo.Abp.UserFriendlyException("Cannot validate taxpayer. E-Invoice API is not connected or token is missing.");
        }

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        return await _lhdnApiClient.SearchTaxpayerAsync(accessToken, idType, idValue, environment);
    }
}
