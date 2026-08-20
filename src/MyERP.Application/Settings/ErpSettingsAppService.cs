using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace MyERP.Settings;

/// <summary>
/// Exposes ERP settings (Stock, Selling, Buying, Manufacturing, Accounts)
/// via API for the Angular settings UI. Uses ABP's ISettingProvider for
/// Global → Tenant → User layering.
/// </summary>
[Authorize(MyERPPermissions.Settings.Default)]
public class ErpSettingsAppService : ApplicationService, IErpSettingsAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;

    public ErpSettingsAppService(ISettingProvider settingProvider, ISettingManager settingManager)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
    }

    /// <summary>Get all settings for a given group (Stock, Selling, Buying, Manufacturing, Accounts, Global).</summary>
    public async Task<Dictionary<string, string>> GetGroupAsync(string group)
    {
        var keys = GetKeysForGroup(group);
        var result = new Dictionary<string, string>();
        foreach (var key in keys)
        {
            var value = await _settingProvider.GetOrNullAsync(key);
            result[key] = value ?? "";
        }
        return result;
    }

    /// <summary>Update multiple settings at once. Stores at Global level (tenant-scoped via ABP).</summary>
    [Authorize(MyERPPermissions.Settings.Edit)]
    public async Task UpdateAsync(Dictionary<string, string> settings)
    {
        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        foreach (var (key, value) in settings)
        {
            var prevValue = await _settingProvider.GetOrNullAsync(key);
            await _settingManager.SetGlobalAsync(key, value);
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "Setting", Guid.Empty,
                "Updated", Guid.Empty,
                key, prevValue ?? "", value, CurrentUser.Id,
                $"Setting {key} updated to '{value}'", CurrentTenant.Id));
        }
    }

    /// <summary>Get a single setting value.</summary>
    public async Task<string> GetAsync(string name)
    {
        return await _settingProvider.GetOrNullAsync(name) ?? "";
    }

    /// <summary>Set a single setting value.</summary>
    [Authorize(MyERPPermissions.Settings.Edit)]
    public async Task SetAsync(string name, string value)
    {
        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        var prevValue = await _settingProvider.GetOrNullAsync(name);
        await _settingManager.SetGlobalAsync(name, value);
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Setting", Guid.Empty,
            "Updated", Guid.Empty,
            name, prevValue ?? "", value, CurrentUser.Id,
            $"Setting {name} updated to '{value}'", CurrentTenant.Id));
    }

    private static string[] GetKeysForGroup(string group) => group switch
    {
        "Stock" => [
            MyERPSettings.Stock.DefaultValuationMethod,
            MyERPSettings.Stock.AllowNegativeStock,
            MyERPSettings.Stock.AutoInsertPriceListRate,
            MyERPSettings.Stock.EnableStockReservation,
            MyERPSettings.Stock.AutoReserveOnPurchase,
            MyERPSettings.Stock.OverDeliveryReceiptAllowance,
            MyERPSettings.Stock.StockFrozenUpToDays,
            MyERPSettings.Stock.ActionIfQualityInspectionNotSubmitted,
            MyERPSettings.Stock.ActionIfQualityInspectionRejected,
            MyERPSettings.Stock.PickSerialAndBatchBasedOn,
            MyERPSettings.Stock.ItemNamingBy,
        ],
        "Selling" => [
            MyERPSettings.Selling.DefaultPriceList,
            MyERPSettings.Selling.SoRequired,
            MyERPSettings.Selling.DnRequired,
            MyERPSettings.Selling.AllowMultipleItems,
            MyERPSettings.Selling.MaintainSameRate,
            MyERPSettings.Selling.MaintainSameRateAction,
            MyERPSettings.Selling.RoleToOverrideStopAction,
            MyERPSettings.Selling.EditablePriceListRate,
            MyERPSettings.Selling.ValidateSellingPrice,
            MyERPSettings.Selling.BlanketOrderAllowance,
            MyERPSettings.Selling.EnableDiscountAccounting,
            MyERPSettings.Selling.AllowZeroQtyInQuotation,
            MyERPSettings.Selling.FallbackToDefaultPriceList,
            MyERPSettings.Selling.CustomerNamingBy,
            MyERPSettings.Selling.CreditControllerRole,
            MyERPSettings.Selling.AllowAgainstMultiplePurchaseOrders,
            MyERPSettings.Selling.EnableProformaInvoice,
            MyERPSettings.Selling.DefaultProformaPrintFormat,
            MyERPSettings.Selling.UseLegacyJsReactivity,
            MyERPSettings.Selling.AllowDeliveryOfOverproducedQty,
        ],
        "Buying" => [
            MyERPSettings.Buying.DefaultPriceList,
            MyERPSettings.Buying.PoRequired,
            MyERPSettings.Buying.PrRequired,
            MyERPSettings.Buying.MaintainSameRate,
            MyERPSettings.Buying.MaintainSameRateAction,
            MyERPSettings.Buying.RoleToOverrideStopAction,
            MyERPSettings.Buying.OverOrderAllowance,
            MyERPSettings.Buying.BackflushSubcontractBasedOn,
            MyERPSettings.Buying.BillForRejectedQty,
            MyERPSettings.Buying.SupplierNamingBy,
            MyERPSettings.Buying.OverTransferAllowance,
            MyERPSettings.Buying.AllowZeroQtyInSupplierQuotation,
            MyERPSettings.Buying.AllowZeroQtyInRequestForQuotation,
        ],
        "Manufacturing" => [
            MyERPSettings.Manufacturing.OverproductionPercentage,
            MyERPSettings.Manufacturing.BackflushRawMaterialsBasedOn,
            MyERPSettings.Manufacturing.MaterialConsumption,
            MyERPSettings.Manufacturing.TransferExtraMaterialsPercentage,
            MyERPSettings.Manufacturing.MinsBetweenOperations,
            MyERPSettings.Manufacturing.CapacityPlanningForDays,
            MyERPSettings.Manufacturing.MakeSerialBatchFromWorkOrder,
            MyERPSettings.Manufacturing.UpdateBomCostsAutomatically,
            MyERPSettings.Manufacturing.AllowOvertime,
            MyERPSettings.Manufacturing.AllowProductionOnHolidays,
            MyERPSettings.Manufacturing.DisableCapacityPlanning,
            MyERPSettings.Manufacturing.JobCardExcessTransfer,
            MyERPSettings.Manufacturing.EnforceTimeLogs,
            MyERPSettings.Manufacturing.AddCorrectiveOpCostInFGValuation,
        ],
        "Accounts" => [
            MyERPSettings.Accounts.OverBillingAllowance,
            MyERPSettings.Accounts.EnableDiscountsAndMargin,
            MyERPSettings.Accounts.RoundRowWiseTax,
            MyERPSettings.Accounts.AutoCreateReversalEntry,
            MyERPSettings.Accounts.BookDeferredEntriesBasedOn,
            MyERPSettings.Accounts.BookStockExpenseGlEntries,
            MyERPSettings.Accounts.RepostLimit,
            MyERPSettings.Accounts.EnableOverdueBillingThreshold,
            MyERPSettings.Accounts.OverdueBillingBypassRole,
            MyERPSettings.Accounts.EnableCommonPartyAccounting,
        ],
        "Global" => [
            MyERPSettings.Global.DefaultCurrency,
            MyERPSettings.Global.DefaultCompany,
            MyERPSettings.Global.Country,
            MyERPSettings.Global.DisableRoundedTotal,
            MyERPSettings.Global.DisableInWords,
            MyERPSettings.Global.HideCurrencySymbol,
            MyERPSettings.Global.UsePostingDateTimeForNamingDocuments,
        ],
        "CRM" => [
            MyERPSettings.CRM.CloseOpportunityAfterDays,
            MyERPSettings.CRM.AutoCreationOfContact,
            MyERPSettings.CRM.CarryForwardCommunicationAndComments,
        ],
        _ => []
    };
}
