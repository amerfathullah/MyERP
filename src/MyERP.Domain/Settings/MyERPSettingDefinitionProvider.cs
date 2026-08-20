using Volo.Abp.Settings;

namespace MyERP.Settings;

public class MyERPSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // ═══════════ Stock Settings ═══════════
        context.Add(
            new SettingDefinition(MyERPSettings.Stock.DefaultValuationMethod, "FIFO"),
            new SettingDefinition(MyERPSettings.Stock.AllowNegativeStock, "false"),
            new SettingDefinition(MyERPSettings.Stock.AutoInsertPriceListRate, "false"),
            new SettingDefinition(MyERPSettings.Stock.EnableStockReservation, "false"),
            new SettingDefinition(MyERPSettings.Stock.AutoReserveOnPurchase, "false"),
            new SettingDefinition(MyERPSettings.Stock.OverDeliveryReceiptAllowance, "0"),
            new SettingDefinition(MyERPSettings.Stock.StockFrozenUpToDays, "0"),
            new SettingDefinition(MyERPSettings.Stock.ActionIfQualityInspectionNotSubmitted, "Stop"),
            new SettingDefinition(MyERPSettings.Stock.ActionIfQualityInspectionRejected, "Stop"),
            new SettingDefinition(MyERPSettings.Stock.PickSerialAndBatchBasedOn, "FIFO"),
            new SettingDefinition(MyERPSettings.Stock.ItemNamingBy, "Item Code")
        );

        // ═══════════ Selling Settings ═══════════
        context.Add(
            new SettingDefinition(MyERPSettings.Selling.SoRequired, "false"),
            new SettingDefinition(MyERPSettings.Selling.DnRequired, "false"),
            new SettingDefinition(MyERPSettings.Selling.AllowMultipleItems, "true"),
            new SettingDefinition(MyERPSettings.Selling.MaintainSameRate, "false"),
            new SettingDefinition(MyERPSettings.Selling.MaintainSameRateAction, "Stop"),
            new SettingDefinition(MyERPSettings.Selling.RoleToOverrideStopAction, ""),
            new SettingDefinition(MyERPSettings.Selling.EditablePriceListRate, "true"),
            new SettingDefinition(MyERPSettings.Selling.ValidateSellingPrice, "false"),
            new SettingDefinition(MyERPSettings.Selling.BlanketOrderAllowance, "0"),
            new SettingDefinition(MyERPSettings.Selling.EnableDiscountAccounting, "false"),
            new SettingDefinition(MyERPSettings.Selling.AllowZeroQtyInQuotation, "false"),
            new SettingDefinition(MyERPSettings.Selling.FallbackToDefaultPriceList, "false"),
            new SettingDefinition(MyERPSettings.Selling.CustomerNamingBy, "Customer Name"),
            new SettingDefinition(MyERPSettings.Selling.CreditControllerRole, "")
        );

        // ═══════════ Buying Settings ═══════════
        context.Add(
            new SettingDefinition(MyERPSettings.Buying.PoRequired, "false"),
            new SettingDefinition(MyERPSettings.Buying.PrRequired, "false"),
            new SettingDefinition(MyERPSettings.Buying.MaintainSameRate, "false"),
            new SettingDefinition(MyERPSettings.Buying.MaintainSameRateAction, "Stop"),
            new SettingDefinition(MyERPSettings.Buying.RoleToOverrideStopAction, ""),
            new SettingDefinition(MyERPSettings.Buying.OverOrderAllowance, "0"),
            new SettingDefinition(MyERPSettings.Buying.BackflushSubcontractBasedOn, "BOM"),
            new SettingDefinition(MyERPSettings.Buying.BillForRejectedQty, "false"),
            new SettingDefinition(MyERPSettings.Buying.SupplierNamingBy, "Supplier Name"),
            new SettingDefinition(MyERPSettings.Buying.OverTransferAllowance, "0")
        );

        // ═══════════ Manufacturing Settings ═══════════
        context.Add(
            new SettingDefinition(MyERPSettings.Manufacturing.OverproductionPercentage, "5"),
            new SettingDefinition(MyERPSettings.Manufacturing.BackflushRawMaterialsBasedOn, "BOM"),
            new SettingDefinition(MyERPSettings.Manufacturing.MaterialConsumption, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.TransferExtraMaterialsPercentage, "0"),
            new SettingDefinition(MyERPSettings.Manufacturing.MinsBetweenOperations, "10"),
            new SettingDefinition(MyERPSettings.Manufacturing.CapacityPlanningForDays, "30"),
            new SettingDefinition(MyERPSettings.Manufacturing.MakeSerialBatchFromWorkOrder, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.UpdateBomCostsAutomatically, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.AllowOvertime, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.AllowProductionOnHolidays, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.DisableCapacityPlanning, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.JobCardExcessTransfer, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.EnforceTimeLogs, "false"),
            new SettingDefinition(MyERPSettings.Manufacturing.AddCorrectiveOpCostInFGValuation, "false")
        );

        // ═══════════ Accounts Settings ═══════════
        context.Add(
            new SettingDefinition(MyERPSettings.Accounts.OverBillingAllowance, "0"),
            new SettingDefinition(MyERPSettings.Accounts.EnableDiscountsAndMargin, "true"),
            new SettingDefinition(MyERPSettings.Accounts.RoundRowWiseTax, "false"),
            new SettingDefinition(MyERPSettings.Accounts.AutoCreateReversalEntry, "false"),
            new SettingDefinition(MyERPSettings.Accounts.BookDeferredEntriesBasedOn, "Days"),
            new SettingDefinition(MyERPSettings.Accounts.BookStockExpenseGlEntries, "false"),
            new SettingDefinition(MyERPSettings.Accounts.RepostLimit, "100"),
            new SettingDefinition(MyERPSettings.Accounts.EnableOverdueBillingThreshold, "false"),
            new SettingDefinition(MyERPSettings.Accounts.OverdueBillingBypassRole, ""),
            new SettingDefinition(MyERPSettings.Accounts.EnableCommonPartyAccounting, "false")
        );

        // ═══════════ Global Defaults ═══════════
        context.Add(
            new SettingDefinition(MyERPSettings.Global.DefaultCurrency, "MYR"),
            new SettingDefinition(MyERPSettings.Global.Country, "Malaysia"),
            new SettingDefinition(MyERPSettings.Global.DisableRoundedTotal, "false"),
            new SettingDefinition(MyERPSettings.Global.DisableInWords, "false"),
            new SettingDefinition(MyERPSettings.Global.HideCurrencySymbol, "false")
        );
    }
}
