namespace MyERP;

public static class MyERPDomainErrorCodes
{
    // Core
    public const string CompanyNameAlreadyExists = "MyERP:00001";
    public const string BranchCodeAlreadyExists = "MyERP:00002";
    public const string CompanyCurrencyLocked = "MyERP:00003";
    public const string CompanyRestrictionBlocked = "MyERP:00004";
    public const string DuplicateRecord = "MyERP:00005";
    public const string DocumentSeriesNotConfigured = "MyERP:18003";
    public const string DocumentNumberGenerationFailed = "MyERP:18004";
    public const string BomReplaceSameBom = "MyERP:18005";
    public const string BomReplaceItemMismatch = "MyERP:18006";
    public const string ItemNotFound = "MyERP:ItemNotFound";
    public const string ExceedsActualQty = "MyERP:ExceedsActualQty";

    // Cross-Cutting
    public const string PrintFormatNotFound = "MyERP:PrintFormatNotFound";
    public const string EntityNotFound = "MyERP:00006";
    public const string DocumentNotEditable = "MyERP:00007";
    public const string ValidationFailed = "MyERP:00008";

    // Document Workflow
    public const string InvalidStatusTransition = "MyERP:01001";

    // Accounting
    public const string UnbalancedJournalEntry = "MyERP:02001";
    public const string FiscalYearClosed = "MyERP:02002";
    public const string AccountIsGroup = "MyERP:02003";
    public const string PaymentTermsPortionMustBe100 = "MyERP:02004";
    public const string InvoiceAlreadySettled = "MyERP:02010";
    public const string PartyNotAllowedOnAccount = "MyERP:02012";
    public const string DuplicateReversalDraft = "MyERP:02063"; // was MyERP:02013 (collided with AccountCannotBeDeleted)
    public const string InvalidBankRuleRegexPattern = "MyERP:02064"; // was hardcoded "MyERP:02013" (collided with AccountCannotBeDeleted)
    public const string DefaultAccountNotConfigured = "MyERP:02065"; // was hardcoded "MyERP:02001" (collided with UnbalancedJournalEntry)
    public const string StockEntryGlPurposeNotSupported = "MyERP:02084";

    // Tax
    public const string NoApplicableTaxRule = "MyERP:03001";
    public const string CreditLimitExceeded = "MyERP:03002";

    // E-Invoice
    public const string EInvoiceSubmissionFailed = "MyERP:04001";
    public const string EInvoiceAlreadySubmitted = "MyERP:04002";
    public const string EInvoiceCancellationFailed = "MyERP:04003";
    public const string CannotCancelInvoiceWithValidEInvoice = "MyERP:04040";
    public const string SupplierOnHold = "MyERP:04004";
    public const string BelowMinimumOrderQty = "MyERP:04005";

    // Import/Export
    public const string UnsupportedEntityType = "MyERP:05001";

    // Approval Workflow
    public const string ApprovalPending = "MyERP:06005"; // was MyERP:06001 (collided with AssetCompanyMismatch)
    public const string ApprovalAlreadyReviewed = "MyERP:06006"; // was MyERP:06002 (collided with AssetCannotBeMoved)

    // Document Conversion
    public const string DocumentMustBeSubmittedForConversion = "MyERP:07007"; // was MyERP:07001 (collided with DuplicatePayrollEntry)
    public const string DocumentAlreadyConverted = "MyERP:07002";
    public const string QuotationExpired = "MyERP:07003";
    public const string DeliveryNoteCustomerMismatch = "MyERP:07004";
    public const string ProjectCustomerMismatch = "MyERP:07006";

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
    public const string SameWarehouseTransfer = "MyERP:05063"; // was hardcoded "MyERP:05017" (collided with raw-string ItemAppService "cannot deactivate item" throws)
    public const string CannotDeleteItem = "MyERP:05018";

    // Barcode Scanner
    public const string BarcodeRequired = "MyERP:05061";
    public const string InsufficientRawMaterial = "MyERP:10008";
    public const string CannotDeleteBOM = "MyERP:10009";

    // Pricing Rule
    public const string PricingRuleAmbiguity = "MyERP:11002";

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
    public const string OverTransfer = "MyERP:08010";

    // Document Guards
    public const string CannotCancelWithPayments = "MyERP:01002";
    public const string FuturePostingDate = "MyERP:01003";
    public const string BaseCurrencyExchangeRateMustBeOne = "MyERP:01004";
    public const string InvalidExchangeRate = "MyERP:01005";
    public const string PaymentEntryUsedInReconciliation = "MyERP:01009";
    public const string CannotCancelWithSubmittedDependents = "MyERP:01010";
    public const string InvalidDateRange = "MyERP:01011";
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
    public const string CustomerNameCannotMatchCustomerGroup = "MyERP:03004";
    public const string CannotDeleteSupplier = "MyERP:04008";
    public const string DuplicateSupplierInvoice = "MyERP:04009";
    public const string DuplicateRfqSupplier = "MyERP:04010";
    public const string SupplierNameCannotMatchSupplierGroup = "MyERP:04030";
    public const string ScorecardCriteriaWeightMustBe100 = "MyERP:04031"; // was hardcoded "MyERP:04009" (collided with DuplicateSupplierInvoice)
    public const string ScorecardStandingRangeInvalid = "MyERP:04032"; // was hardcoded "MyERP:04010" (collided with DuplicateRfqSupplier)
    public const string PurchaseOrderItemNotFoundForDelivery = "MyERP:04033"; // was hardcoded "MyERP:04016" (collided with DropShipItemNotFound)
    public const string MaterialRequestItemNotFound = "MyERP:04034"; // was hardcoded "MyERP:04016" (collided with DropShipItemNotFound)
    public const string DuplicateMaterialRequestItemSelection = "MyERP:04035"; // was hardcoded "MyERP:04019" (collided with UpdateItemsQtyBelowReceived)
    public const string QtyExceedsPendingMaterialRequest = "MyERP:04036"; // was hardcoded "MyERP:04020" (collided with UpdateItemsRateBelowBilled)
    public const string SupplierQuotationHasNoItems = "MyERP:04037"; // was hardcoded "MyERP:04017" (collided with DropShipQtyReductionExceeded)
    public const string PurchaseInvoiceOnHold = "MyERP:04038";
    public const string ReleaseDateMustBeFuture = "MyERP:04039";
    public const string DeliveryWarehouseNotReserved = "MyERP:05058";
    public const string CaseNumberRangeOverlap = "MyERP:05059";
    public const string DeliveryNoteNotFullyPacked = "MyERP:05060";
    public const string CannotCancelWithDraftDependents = "MyERP:01019";
    public const string DuplicateCustomerPoNumber = "MyERP:01020";
    public const string PartyCannotRepresentOwnCompany = "MyERP:03005";

    // Timesheet Billing
    public const string NoUnbilledTimesheetEntries = "MyERP:15001";
    public const string AssetMissingRequiredField = "MyERP:15002";
    public const string AssetDisposalAccountMissing = "MyERP:15003";
    public const string LocationCannotBeDeleted = "MyERP:15004"; // was hardcoded "MyERP:15002" (collided with AssetMissingRequiredField)
    public const string AssetCategoryCannotBeDeleted = "MyERP:15005"; // was hardcoded "MyERP:15002" (collided with AssetMissingRequiredField)
    public const string TimesheetOverlappingTimeLog = "MyERP:15006"; // was hardcoded "MyERP:15002" (collided with AssetMissingRequiredField)
    public const string AssetShiftInsufficientUnassignedPeriods = "MyERP:15007"; // was hardcoded "MyERP:15003" (collided with AssetDisposalAccountMissing)

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
    public const string ThreeWayMatchingFailed = "MyERP:04015";
    public const string PurchaseOrderLinkRequired = "MyERP:04028";
    public const string PurchaseReceiptLinkRequired = "MyERP:04029";
    public const string DropShipItemNotFound = "MyERP:04016";
    public const string DropShipQtyReductionExceeded = "MyERP:04017";
    public const string DropShipQtyIncreaseExceeded = "MyERP:04018";
    public const string UpdateItemsQtyBelowReceived = "MyERP:04019";
    public const string UpdateItemsRateBelowBilled = "MyERP:04020";

    // Subcontracting BOM
    public const string SubcontractingBomInvalidQty = "MyERP:04021";
    public const string SubcontractingBomFinishedGoodDisabled = "MyERP:04022";
    public const string SubcontractingBomFinishedGoodNotStockItem = "MyERP:04023";
    public const string SubcontractingBomFinishedGoodNoDefaultBom = "MyERP:04024";
    public const string SubcontractingBomServiceItemDisabled = "MyERP:04025";
    public const string SubcontractingBomServiceItemIsStockItem = "MyERP:04026";
    public const string SubcontractingBomFinishedGoodAlreadyActive = "MyERP:04027";

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
    public const string JobCardTimeLogRequired = "MyERP:10031";

    // Sales — Extended
    public const string InstallationDateBeforeDelivery = "MyERP:03016";
    public const string InstallationQtyExceedsDeliveryNote = "MyERP:03039"; // was MyERP:03032 (collided with TaxWithholdingCategoryNotFound), then MyERP:03037 (collided with MaxDiscountExceeded)

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

    // Returns & Invoices — Extended
    public const string ReturnAccountMismatch = "MyERP:08008";
    public const string ReturnWithStockZeroQty = "MyERP:08009";
    public const string ReturnRateExceedsOriginal = "MyERP:08011";
    public const string InvoiceOnHold = "MyERP:08012";

    // Payment Entry — Term Allocation
    public const string PaymentTermRequired = "MyERP:02061"; // was MyERP:02026 (collided with MandatoryDimensionMissing)
    public const string PaymentTermOutstandingExceeded = "MyERP:02062"; // was MyERP:02027 (collided with DimensionValueRestricted)

    // Serial and Batch Bundle
    public const string BundleQtyMismatch = "MyERP:05032";

    // Item Standard Cost
    public const string StandardCostEffectiveDateInFuture = "MyERP:05033";
    public const string StandardCostEffectiveDateBeforeLastSle = "MyERP:05034";
    public const string StandardCostCannotCancel = "MyERP:05035";

    // Repost Item Valuation
    public const string RepostAlreadyInProgress = "MyERP:05036";

    // Transit Transfer
    public const string NoTransitWarehouseConfigured = "MyERP:05037";
    public const string InvalidTransitSourceEntry = "MyERP:05038";
    public const string TransitSourceNotPosted = "MyERP:05039";
    public const string TransitReceivingEntryExists = "MyERP:05040";

    // Repack / Disassemble
    public const string RepackMissingItems = "MyERP:05041";
    public const string RepackMultiFgManualRate = "MyERP:05042";
    public const string DisassembleSourceNotFound = "MyERP:05043";
    public const string DisassembleCrossWorkOrder = "MyERP:05044";
    public const string DisassembleQtyExceedsSource = "MyERP:05045";
    public const string DisassembleScaleFactorMismatch = "MyERP:05046";
    public const string StockReconciliationMissingExpenseAccount = "MyERP:05047";
    public const string ManufactureMultiFgItemsNotAllowed = "MyERP:05048";

    // Manufacturing
    public const string AllMaterialsAlreadyTransferred = "MyERP:10013";
    public const string MaterialConsumptionDisabled = "MyERP:10014";
    public const string DoubleConsumption = "MyERP:10015";
    public const string ConsumedQtyExceedsTransferred = "MyERP:10016";
    public const string BomFgCannotBeSecondaryItem = "MyERP:10017";
    public const string InvalidProcessLossPercentage = "MyERP:10018";
    public const string SecondaryItemCostAllocationInvalid = "MyERP:10019";
    public const string PreviousOperationNotManufactured = "MyERP:10020";
    public const string CompletionSplitMismatch = "MyERP:10021";

    // Party Link
    public const string PartyCannotLinkToSelf = "MyERP:00009";

    // Coupon Code
    public const string CouponCodeMaxUsageReached = "MyERP:03017";
    public const string PricingRuleNotFound = "MyERP:03040";
    public const string CouponCodeNotFound = "MyERP:03041";
    public const string CouponCodeInvalid = "MyERP:03042";

    // Overdue Billing
    public const string OverdueBillingThresholdExceeded = "MyERP:03043";

    // Lead
    public const string DuplicateLeadEmail = "MyERP:03022";

    // Sales Partner / Commission
    public const string InvalidCommissionRate = "MyERP:03023";

    // SO Update Items guards
    public const string SoItemQtyBelowDelivered = "MyERP:03024";
    public const string SoItemRateBelowBilled = "MyERP:03025";
    public const string SalesTeamPercentageMustTotal100 = "MyERP:03026";

    // Bank Transaction — Auto-Reconcile
    public const string BankTransactionAlreadyReconciled = "MyERP:02048";

    // Bank Clearance
    public const string ClearanceDateBeforePostingDate = "MyERP:02049";

    // POS
    public const string NoPosOpeningEntry = "MyERP:16003";
    public const string PosProfileAlreadyOpen = "MyERP:16001";
    public const string PosUserAlreadyHasSession = "MyERP:16002";
    public const string PosProfileHasOpenSession = "MyERP:16004";
    public const string PosOpeningHasUnconsolidatedInvoices = "MyERP:16005";
    public const string PosClosingCancelBlockedByOpenSession = "MyERP:16006";

    // Project
    public const string ProjectPercentOutOfRange = "MyERP:13003";
    public const string ProjectTemplateDependencyNotInTemplate = "MyERP:13004";

    // Account
    public const string StockAccountTypeChangeLocked = "MyERP:02085";

    // CRM — Prospect
    public const string ProspectAlreadyConverted = "MyERP:17001";

    // CRM — Contract
    public const string ContractAlreadyActive = "MyERP:17002";

    // Cost Center Allocation
    public const string CostCenterAllocationSelfReference = "MyERP:02038";
    public const string CostCenterAllocationPercentageOutOfRange = "MyERP:02039";
    public const string CostCenterAllocationDuplicate = "MyERP:02040";
    public const string CostCenterAllocationNoEntries = "MyERP:02041";
    public const string CostCenterAllocationPercentagesNot100 = "MyERP:02042";
    public const string CostCenterAllocationCycleDetected = "MyERP:02043";
    public const string CostCenterAllocationValidFromBeforeGL = "MyERP:02044";

    // Financial Report Template
    public const string FormulaValidationFailed = "MyERP:02045";
    public const string CannotDeleteStandardTemplate = "MyERP:02046";

    // Month End Close
    public const string CannotFreezeFutureDate = "MyERP:02047";

    // Finance Book
    public const string DuplicateDefaultFinanceBook = "MyERP:02088";

    // Authorization Control
    public const string NoApprover = "MyERP:01013";
    public const string SelfApproval = "MyERP:01014";
    public const string DiscountExceeds100 = "MyERP:01015";
    public const string CustomerwiseNeedsCustomer = "MyERP:01016";
    public const string AuthorizationBlocked = "MyERP:01017";
    public const string RecipientEmailRequired = "MyERP:01018";

    // Support — Service Level Agreement
    public const string DuplicateDefaultServiceLevelAgreement = "MyERP:18001";
    public const string ServiceLevelPriorityNotFound = "MyERP:18002";

    // Payment Order
    public const string PaymentOrderHasNoReferences = "MyERP:02050";

    // Unreconcile Payment
    public const string UnreconcilePaymentHasNoAllocations = "MyERP:02051";

    // CRM — Appointment
    public const string AppointmentSlotFull = "MyERP:17003";
    public const string AppointmentNotVerified = "MyERP:17004";
    public const string AppointmentOutsideServiceWindow = "MyERP:17005";

    // CRM — Email Campaign
    public const string EmailCampaignStartDateInPast = "MyERP:17006";
    public const string EmailCampaignDuplicateActive = "MyERP:17007";

    // Promotional Scheme
    public const string PromotionalSchemeRequiresSellingOrBuying = "MyERP:03018";
    public const string PromotionalSchemeRequiresSlabs = "MyERP:03019";
    public const string PromotionalSchemeRecursiveWithMixedConditions = "MyERP:03020";
    public const string PromotionalSchemeApplicableForRequiresParty = "MyERP:03021";

    // Manufacturing — Downtime Entry
    public const string DowntimeEntryToTimeBeforeFromTime = "MyERP:10022";

    // Manufacturing — BOM Creator
    public const string BomCreatorRequiresItems = "MyERP:10023";
    public const string BomCreatorAlreadyProcessed = "MyERP:10024";

    // Share Transfer
    public const string ShareTransferSellerBuyerSame = "MyERP:02052";
    public const string ShareTransferSharesAlreadyExist = "MyERP:02053";
    public const string ShareTransferSharesDoNotExist = "MyERP:02054";
    public const string ShareTransferCountMismatch = "MyERP:02055";
    public const string ShareTransferAmountMismatch = "MyERP:02056";
    public const string ShareTransferFolioMismatch = "MyERP:02057";
    public const string ShareTransferMissingParty = "MyERP:02058";
    public const string ShareTransferMissingAccount = "MyERP:02059";

    // Monthly Distribution
    public const string MonthlyDistributionMustTotal100 = "MyERP:02060";

    // Master Production Schedule
    public const string MasterProductionScheduleHasNoItems = "MyERP:10025";

    // Tax Withholding Category
    public const string TaxWithholdingRateDateRangeInvalid = "MyERP:03027";
    public const string TaxWithholdingRateOverlap = "MyERP:03028";
    public const string TaxWithholdingNoApplicableRate = "MyERP:03029";
    public const string TaxWithholdingNoAccountForCompany = "MyERP:03030";
    public const string TaxWithholdingDuplicateCompanyAccount = "MyERP:03031";
    public const string TaxWithholdingCategoryNotFound = "MyERP:03032";

    // Sales Forecast
    public const string SalesForecastHasNoSelectedItems = "MyERP:10026";
    public const string SalesForecastAlreadyUsedForMps = "MyERP:10027";
    public const string WorkOrderWarehouseCompanyMismatch = "MyERP:10028"; // was hardcoded "MyERP:10020" (collided with PreviousOperationNotManufactured)
    public const string BomOperationSequenceOutOfOrder = "MyERP:10029"; // was hardcoded "MyERP:10020" (collided with PreviousOperationNotManufactured)
    public const string BomHasNoRouting = "MyERP:10030"; // was hardcoded "MyERP:10017" (collided with BomFgCannotBeSecondaryItem)
    public const string PeriodClosingAccountTypeInvalid = "MyERP:02066"; // was hardcoded "MyERP:02031" (collided with OpeningBalanceGroupAccountBlocked)
    public const string PeriodClosingAccountCurrencyMismatch = "MyERP:02067"; // was hardcoded "MyERP:02032" (collided with OpeningBalanceNoTempAccount)

    // Invoice Discounting
    public const string InvoiceAlreadyDiscounted = "MyERP:02068";
    public const string InvoiceDiscountingOutstandingExceeded = "MyERP:02069";
    public const string InvoiceDiscountingLoanTermsRequired = "MyERP:02070";
    public const string InvoiceDiscountingNoInvoices = "MyERP:02071";

    // General Ledger — P&L cost center enforcement
    public const string CostCenterRequiredForPlAccount = "MyERP:02072";
    public const string RoundOffAccountNotConfigured = "MyERP:02073";

    // Repost Accounting Ledger
    public const string RepostVoucherTypeNotAllowed = "MyERP:02074";
    public const string RepostNoVouchersSelected = "MyERP:02075";
    public const string RepostTooManyVouchers = "MyERP:02076";
    public const string RepostDuplicateVoucher = "MyERP:02077";
    public const string RepostVoucherNotPosted = "MyERP:02078";
    public const string RepostVoucherInClosedFiscalYear = "MyERP:02079";
    public const string RepostAccountingLedgerAlreadyInProgress = "MyERP:02080";
    public const string RepostInvalidStatusForStart = "MyERP:02081";
    public const string RepostVoucherHasDeferredAccounting = "MyERP:02082";
    public const string ProcessPaymentReconciliationAlreadyActive = "MyERP:02083";

    // Stock Reservation — Settings
    public const string StockReservationDisabled = "MyERP:05062";

    // Selling Settings — Mandatory Linkage
    public const string SalesOrderLinkRequired = "MyERP:03033";
    public const string DeliveryNoteLinkRequired = "MyERP:03034";

    // Maintain Same Rate (Buying/Selling Settings)
    public const string RateMismatchWithReferenceDoc = "MyERP:03035";
    public const string InvalidDiscountPercentage = "MyERP:03036";
    public const string MaxDiscountExceeded = "MyERP:03037";
    public const string ProformaInvoiceDisabled = "MyERP:03038";

    // Item Alternative
    public const string ItemDoesNotAllowAlternatives = "MyERP:05049";
    public const string AlternativeItemStockMismatch = "MyERP:05050";
    public const string DuplicateItemAlternative = "MyERP:05051";

    // Asset Movement & Capitalization
    public const string AssetCompanyMismatch = "MyERP:06001";
    public const string AssetCannotBeMoved = "MyERP:06002";
    public const string AssetMovementSameLocationAndCustodian = "MyERP:06003";
    public const string ConsumedAssetCannotBeTargetAsset = "MyERP:06004";

    // General Company
    public const string CompanyMismatch = "MyERP:01012";

    // Pricing Rules
    public const string AmbiguousPricingRule = "MyERP:11001";

    // Payroll
    public const string DuplicatePayrollEntry = "MyERP:07001";

    // Warehouse
    public const string InvalidParentWarehouse = "MyERP:05052";
    public const string StockClosingDateAlreadyCovered = "MyERP:05053"; // was hardcoded "MyERP:05029" (collided with UomMustBeWholeNumber)
    public const string AttributeRangeInvalid = "MyERP:05054"; // was hardcoded "MyERP:05019" (collided with WarehouseCannotBeDeleted)
    public const string AttributeIncrementInvalid = "MyERP:05055"; // was hardcoded "MyERP:05020" (never registered)
    public const string NumericAttributeCannotHaveTextValues = "MyERP:05056"; // was hardcoded "MyERP:05021" (never registered)
    public const string DuplicateAttributeValue = "MyERP:05057"; // was hardcoded "MyERP:05022" (never registered)

    // Party Validation (gotchas #1004, #1130)
    public const string PartyDisabled = "MyERP:01022";
    public const string PartyFrozen = "MyERP:01021";
    public const string ItemAccountCannotBePartyAccount = "MyERP:02086";
    public const string PartyAccountCurrencyMismatch = "MyERP:02087";
    public const string StaleExchangeRate = "MyERP:02089"; // was "MyERP:02088" (collided with DuplicateDefaultFinanceBook)
}
