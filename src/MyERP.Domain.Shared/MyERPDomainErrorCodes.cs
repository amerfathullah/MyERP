namespace MyERP;

public static class MyERPDomainErrorCodes
{
    // Core
    public const string CompanyNameAlreadyExists = "MyERP:00001";
    public const string BranchCodeAlreadyExists = "MyERP:00002";
    public const string CompanyCurrencyLocked = "MyERP:00003";

    // Document Workflow
    public const string InvalidStatusTransition = "MyERP:01001";

    // Accounting
    public const string UnbalancedJournalEntry = "MyERP:02001";
    public const string FiscalYearClosed = "MyERP:02002";
    public const string AccountIsGroup = "MyERP:02003";
    public const string PaymentTermsPortionMustBe100 = "MyERP:02004";
    public const string InvoiceAlreadySettled = "MyERP:02010";
    public const string PartyNotAllowedOnAccount = "MyERP:02012";

    // Tax
    public const string NoApplicableTaxRule = "MyERP:03001";
    public const string CreditLimitExceeded = "MyERP:03002";

    // E-Invoice
    public const string EInvoiceSubmissionFailed = "MyERP:04001";
    public const string EInvoiceAlreadySubmitted = "MyERP:04002";
    public const string EInvoiceCancellationFailed = "MyERP:04003";
    public const string SupplierOnHold = "MyERP:04004";
    public const string BelowMinimumOrderQty = "MyERP:04005";

    // Import/Export
    public const string UnsupportedEntityType = "MyERP:05001";

    // Approval Workflow
    public const string ApprovalPending = "MyERP:06001";
    public const string ApprovalAlreadyReviewed = "MyERP:06002";

    // Document Conversion
    public const string DocumentMustBeSubmittedForConversion = "MyERP:07001";
    public const string DocumentAlreadyConverted = "MyERP:07002";
    public const string QuotationExpired = "MyERP:07003";

    // Manufacturing
    public const string PlannedEndDateBeforeStartDate = "MyERP:10001";
    public const string ActualEndDateBeforeStartDate = "MyERP:10002";
    public const string MaterialRequestAlreadyExists = "MyERP:10003";
    public const string ProductionPlanHasNoItems = "MyERP:10004";
    public const string ProductionPlanWorkOrdersAlreadyGenerated = "MyERP:10005";

    // Inventory
    public const string InsufficientStock = "MyERP:05002";
    public const string QualityInspectionHasNoReadings = "MyERP:05003";
    public const string LandedCostHasNoItems = "MyERP:05004";
    public const string LandedCostHasNoCharges = "MyERP:05005";
    public const string LandedCostDistributionMismatch = "MyERP:05006";
    public const string StockFrozenPeriod = "MyERP:05007";
    public const string AccountingPeriodClosed = "MyERP:05008";

    // Budget
    public const string BudgetHasNoAccounts = "MyERP:02005";
    public const string BudgetLevel1RequiresLevel2 = "MyERP:02006";
    public const string BudgetLevel2RequiresLevel3 = "MyERP:02007";
    public const string BudgetExceeded = "MyERP:02008";
    public const string OverAllocation = "MyERP:02009";

    // Quality Inspection
    public const string QualityInspectionRequired = "MyERP:05009";
    public const string QualityInspectionRejected = "MyERP:05010";

    // Batch/Serial
    public const string BatchExpired = "MyERP:05011";
    public const string BatchDisabled = "MyERP:05012";
    public const string GroupWarehouseCannotReceiveStock = "MyERP:05014";
    public const string ValuationMethodChangeLocked = "MyERP:05015";
    public const string MissingWarehouse = "MyERP:05016";
    public const string SameWarehouseTransfer = "MyERP:05017";
    public const string CannotDeleteItem = "MyERP:05018";
    public const string InsufficientRawMaterial = "MyERP:10008";
    public const string CannotDeleteBOM = "MyERP:10009";

    // Pricing Rule
    public const string PricingRuleAmbiguity = "MyERP:11001";

    // Inter-Company
    public const string InterCompanyPartyMismatch = "MyERP:09001";

    // Returns
    public const string ReturnQtyMustBeNegative = "MyERP:08001";
    public const string ReturnMustReferenceOriginal = "MyERP:08002";
    public const string ReturnExchangeRateMismatch = "MyERP:08003";
    public const string ReturnQtyExceedsOriginal = "MyERP:08004";

    // Over-delivery/receipt/billing
    public const string OverDelivery = "MyERP:08005";
    public const string OverReceipt = "MyERP:08006";
    public const string OverBilling = "MyERP:08007";

    // Document Guards
    public const string CannotCancelWithPayments = "MyERP:01002";
    public const string FuturePostingDate = "MyERP:01003";
    public const string BaseCurrencyExchangeRateMustBeOne = "MyERP:01004";
    public const string InvalidExchangeRate = "MyERP:01005";
    public const string PaymentEntryUsedInReconciliation = "MyERP:01009";
    public const string CannotCancelWithSubmittedDependents = "MyERP:01010";
    public const string PriorFiscalYearNotClosed = "MyERP:02011";

    // Item Validation
    public const string ItemInactive = "MyERP:05013";

    // Opening Invoice
    public const string OpeningInvoiceCannotUpdateStock = "MyERP:01006";

    // Input Validation
    public const string DocumentMustHaveItems = "MyERP:01007";
    public const string AmountMustBePositive = "MyERP:01008";

    // Manufacturing
    public const string WorkOrderOverproduction = "MyERP:10006";
    public const string BomCycleDetected = "MyERP:10007";

    // Projects
    public const string CircularDependencyDetected = "MyERP:13001";
    public const string DependenciesIncomplete = "MyERP:13002";

    // Subscription
    public const string SubscriptionNotActive = "MyERP:12001";
    public const string SubscriptionHasNoPlans = "MyERP:12002";

    // HR
    public const string InsufficientLeaveBalance = "MyERP:14001";
    public const string CannotDeleteUsedAllocation = "MyERP:14002";

    // Supplier Scorecard
    public const string ScorecardBlockedPO = "MyERP:04006";
    public const string ScorecardBlockedRFQ = "MyERP:04007";
    public const string CannotDeleteCustomer = "MyERP:03003";
    public const string CannotDeleteSupplier = "MyERP:04008";

    // Timesheet Billing
    public const string NoUnbilledTimesheetEntries = "MyERP:15001";
}
