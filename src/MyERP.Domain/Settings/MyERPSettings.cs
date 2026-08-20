namespace MyERP.Settings;

/// <summary>
/// ABP Settings keys for all ERP configuration.
/// Per ERPNext: settings are singleton entities per company (Global Defaults, Stock Settings, etc.)
/// MyERP uses ABP's ISettingProvider which supports Global → Tenant → User layering.
/// </summary>
public static class MyERPSettings
{
    private const string Prefix = "MyERP";

    // ═══════════════════════════════════════════════════
    // Stock Settings (35+ flags per ERPNext)
    // ═══════════════════════════════════════════════════
    public static class Stock
    {
        private const string G = Prefix + ".Stock";
        public const string DefaultValuationMethod = G + ".DefaultValuationMethod";
        public const string DefaultWarehouse = G + ".DefaultWarehouse";
        public const string AllowNegativeStock = G + ".AllowNegativeStock";
        public const string AutoInsertPriceListRate = G + ".AutoInsertPriceListRate";
        public const string EnableStockReservation = G + ".EnableStockReservation";
        public const string AutoReserveOnPurchase = G + ".AutoReserveOnPurchase";
        public const string OverDeliveryReceiptAllowance = G + ".OverDeliveryReceiptAllowance";
        public const string StockFrozenUpTo = G + ".StockFrozenUpTo";
        public const string StockFrozenUpToDays = G + ".StockFrozenUpToDays";
        public const string StockAuthRole = G + ".StockAuthRole";
        public const string ActionIfQualityInspectionNotSubmitted = G + ".ActionIfQINotSubmitted";
        public const string ActionIfQualityInspectionRejected = G + ".ActionIfQIRejected";
        public const string PickSerialAndBatchBasedOn = G + ".PickSerialBatchBasedOn";
        public const string ItemNamingBy = G + ".ItemNamingBy";
        public const string SampleRetentionWarehouse = G + ".SampleRetentionWarehouse";
    }

    // ═══════════════════════════════════════════════════
    // Selling Settings (26+ flags per ERPNext)
    // ═══════════════════════════════════════════════════
    public static class Selling
    {
        private const string G = Prefix + ".Selling";
        public const string DefaultPriceList = G + ".DefaultPriceList";
        public const string SoRequired = G + ".SoRequired";
        public const string DnRequired = G + ".DnRequired";
        public const string AllowMultipleItems = G + ".AllowMultipleItems";
        public const string MaintainSameRate = G + ".MaintainSameRate";
        public const string MaintainSameRateAction = G + ".MaintainSameRateAction";
        public const string RoleToOverrideStopAction = G + ".RoleToOverrideStopAction";
        public const string EditablePriceListRate = G + ".EditablePriceListRate";
        public const string ValidateSellingPrice = G + ".ValidateSellingPrice";
        public const string BlanketOrderAllowance = G + ".BlanketOrderAllowance";
        public const string EnableDiscountAccounting = G + ".EnableDiscountAccounting";
        public const string AllowZeroQtyInQuotation = G + ".AllowZeroQtyInQuotation";
        public const string FallbackToDefaultPriceList = G + ".FallbackToDefaultPriceList";
        public const string CustomerNamingBy = G + ".CustomerNamingBy";
        public const string CreditControllerRole = G + ".CreditControllerRole";
        public const string AllowAgainstMultiplePurchaseOrders = G + ".AllowAgainstMultiplePOs";
        public const string EnableProformaInvoice = G + ".EnableProformaInvoice";
        public const string DefaultProformaPrintFormat = G + ".DefaultProformaPrintFormat";
        public const string UseLegacyJsReactivity = G + ".UseLegacyJsReactivity";
        public const string AllowDeliveryOfOverproducedQty = G + ".AllowDeliveryOfOverproducedQty";
    }

    // ═══════════════════════════════════════════════════
    // Buying Settings (22+ flags per ERPNext)
    // ═══════════════════════════════════════════════════
    public static class Buying
    {
        private const string G = Prefix + ".Buying";
        public const string DefaultPriceList = G + ".DefaultPriceList";
        public const string PoRequired = G + ".PoRequired";
        public const string PrRequired = G + ".PrRequired";
        public const string MaintainSameRate = G + ".MaintainSameRate";
        public const string MaintainSameRateAction = G + ".MaintainSameRateAction";
        public const string RoleToOverrideStopAction = G + ".RoleToOverrideStopAction";
        public const string OverOrderAllowance = G + ".OverOrderAllowance";
        public const string BackflushSubcontractBasedOn = G + ".BackflushSubcontractBasedOn";
        public const string BillForRejectedQty = G + ".BillForRejectedQty";
        public const string SupplierNamingBy = G + ".SupplierNamingBy";
        public const string OverTransferAllowance = G + ".OverTransferAllowance";
    }

    // ═══════════════════════════════════════════════════
    // Manufacturing Settings (14 critical flags)
    // ═══════════════════════════════════════════════════
    public static class Manufacturing
    {
        private const string G = Prefix + ".Manufacturing";
        public const string OverproductionPercentage = G + ".OverproductionPercentage";
        public const string BackflushRawMaterialsBasedOn = G + ".BackflushBasedOn";
        public const string MaterialConsumption = G + ".MaterialConsumption";
        public const string TransferExtraMaterialsPercentage = G + ".TransferExtraPct";
        public const string MinsBetweenOperations = G + ".MinsBetweenOps";
        public const string CapacityPlanningForDays = G + ".CapacityPlanDays";
        public const string MakeSerialBatchFromWorkOrder = G + ".MakeSerialBatchFromWO";
        public const string UpdateBomCostsAutomatically = G + ".AutoUpdateBomCosts";
        public const string AllowOvertime = G + ".AllowOvertime";
        public const string AllowProductionOnHolidays = G + ".AllowHolidayProduction";
        public const string DisableCapacityPlanning = G + ".DisableCapacityPlanning";
        public const string JobCardExcessTransfer = G + ".JobCardExcessTransfer";
        public const string EnforceTimeLogs = G + ".EnforceTimeLogs";
        public const string AddCorrectiveOpCostInFGValuation = G + ".CorrectiveOpCostInFG";
    }

    // ═══════════════════════════════════════════════════
    // Accounts Settings
    // ═══════════════════════════════════════════════════
    public static class Accounts
    {
        private const string G = Prefix + ".Accounts";
        public const string OverBillingAllowance = G + ".OverBillingAllowance";
        public const string EnableDiscountsAndMargin = G + ".EnableDiscountsAndMargin";
        public const string RoundRowWiseTax = G + ".RoundRowWiseTax";
        public const string AutoCreateReversalEntry = G + ".AutoCreateReversalEntry";
        public const string BookDeferredEntriesBasedOn = G + ".DeferredBasedOn";
        public const string BookStockExpenseGlEntries = G + ".BookStockExpenseGL";
        public const string RepostLimit = G + ".RepostLimit";
        public const string EnableOverdueBillingThreshold = G + ".EnableOverdueBilling";
        public const string OverdueBillingBypassRole = G + ".OverdueBillingBypassRole";
        public const string EnableCommonPartyAccounting = G + ".EnableCommonPartyAccounting";
    }

    // ═══════════════════════════════════════════════════
    // Global Defaults
    // ═══════════════════════════════════════════════════
    public static class Global
    {
        private const string G = Prefix + ".Global";
        public const string DefaultCurrency = G + ".DefaultCurrency";
        public const string DefaultCompany = G + ".DefaultCompany";
        public const string Country = G + ".Country";
        public const string DisableRoundedTotal = G + ".DisableRoundedTotal";
        public const string DisableInWords = G + ".DisableInWords";
        public const string HideCurrencySymbol = G + ".HideCurrencySymbol";
    }

    // ═══════════════════════════════════════════════════
    // CRM Settings
    // ═══════════════════════════════════════════════════
    public static class CRM
    {
        private const string G = Prefix + ".CRM";
        public const string CloseOpportunityAfterDays = G + ".CloseOpportunityAfterDays";
        public const string AutoCreationOfContact = G + ".AutoCreationOfContact";
        public const string CarryForwardCommunicationAndComments = G + ".CarryForwardCommunication";
    }
}

