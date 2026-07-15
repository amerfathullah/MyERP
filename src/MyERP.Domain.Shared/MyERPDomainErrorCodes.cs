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

    // Payment Entry
    public const string DuplicatePaymentReference = "MyERP:02021";

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
    public const string LeaveOverlap = "MyERP:14004";

    // Supplier Scorecard
    public const string ScorecardBlockedPO = "MyERP:04006";
    public const string ScorecardBlockedRFQ = "MyERP:04007";
    public const string CannotDeleteCustomer = "MyERP:03003";
    public const string CannotDeleteSupplier = "MyERP:04008";
    public const string DuplicateSupplierInvoice = "MyERP:04009";
    public const string DuplicateRfqSupplier = "MyERP:04010";

    // Timesheet Billing
    public const string NoUnbilledTimesheetEntries = "MyERP:15001";
    public const string AssetMissingRequiredField = "MyERP:15002";

    // Accounting — Additional
    public const string AccountCannotBeDeleted = "MyERP:02013";

    // Inventory — Additional
    public const string WarehouseCannotBeDeleted = "MyERP:05019";

    // E-Invoice — Additional
    public const string EInvoiceValidationFailed = "MyERP:EInvoice:00010";

    // Purchasing — Buying Controller Validations
    public const string PostingDateBeforePODate = "MyERP:04011";
    public const string AssetExistsOnReturnDocument = "MyERP:04012";
    public const string FromWarehouseEqualsTargetWarehouse = "MyERP:04013";
    public const string FromWarehouseOnSubcontractedDocument = "MyERP:04014";

    // Bank Transaction
    public const string BankTransactionCurrencyMismatch = "MyERP:02022";
    public const string ExcludedFeeExceedsDeposit = "MyERP:02023";
    public const string BidirectionalFeeTransaction = "MyERP:02024";
    public const string IncludedFeeExceedsWithdrawal = "MyERP:02025";

    // Selling Validations
    public const string SellingPriceBelowCost = "MyERP:03015";

    // Accounting Dimensions
    public const string MandatoryDimensionMissing = "MyERP:02026";
    public const string DimensionValueRestricted = "MyERP:02027";

    // Chart of Accounts Import
    public const string ChartOfAccountsImportBlocked = "MyERP:02028";
    public const string DuplicateAccountCode = "MyERP:02029";

    // Opening Balance
    public const string OpeningBalanceOnlyBSAccounts = "MyERP:02030";
    public const string OpeningBalanceGroupAccountBlocked = "MyERP:02031";
    public const string OpeningBalanceNoTempAccount = "MyERP:02032";
    public const string OpeningBalanceNoEntries = "MyERP:02033";

    // Manufacturing — Extended
    public const string ItemHasVariants = "MyERP:10010";
    public const string BomInactive = "MyERP:10011";
    public const string WorkstationCapacityExceeded = "MyERP:10012";

    // Sales — Extended
    public const string InstallationDateBeforeDelivery = "MyERP:03016";

    // Stock Entry — Extended
    public const string ExcessMaterialTransfer = "MyERP:05030";

    // UOM
    public const string UomMustBeWholeNumber = "MyERP:05029";

    // Inventory — Stock Closing
    public const string NoBalanceEntries = "MyERP:05028";

    // Stock Reservation
    public const string InsufficientStockForReservation = "MyERP:05031";

    // HR — Extended
    public const string AdvanceExceedsPayment = "MyERP:14005";

    // Returns — Extended
    public const string ReturnAccountMismatch = "MyERP:08008";
    public const string ReturnWithStockZeroQty = "MyERP:08009";

    // Payment Entry — Term Allocation
    public const string PaymentTermRequired = "MyERP:02026";
    public const string PaymentTermOutstandingExceeded = "MyERP:02027";
}
