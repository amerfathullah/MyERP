namespace MyERP;

public static class MyERPDomainErrorCodes
{
    // Core
    public const string CompanyNameAlreadyExists = "MyERP:00001";
    public const string BranchCodeAlreadyExists = "MyERP:00002";

    // Document Workflow
    public const string InvalidStatusTransition = "MyERP:01001";

    // Accounting
    public const string UnbalancedJournalEntry = "MyERP:02001";
    public const string FiscalYearClosed = "MyERP:02002";
    public const string AccountIsGroup = "MyERP:02003";

    // Tax
    public const string NoApplicableTaxRule = "MyERP:03001";

    // E-Invoice
    public const string EInvoiceSubmissionFailed = "MyERP:04001";
    public const string EInvoiceAlreadySubmitted = "MyERP:04002";
    public const string EInvoiceCancellationFailed = "MyERP:04003";

    // Import/Export
    public const string UnsupportedEntityType = "MyERP:05001";

    // Approval Workflow
    public const string ApprovalPending = "MyERP:06001";
    public const string ApprovalAlreadyReviewed = "MyERP:06002";

    // Document Conversion
    public const string DocumentMustBeSubmittedForConversion = "MyERP:07001";
    public const string DocumentAlreadyConverted = "MyERP:07002";

    // Manufacturing
    public const string PlannedEndDateBeforeStartDate = "MyERP:10001";
    public const string ActualEndDateBeforeStartDate = "MyERP:10002";
    public const string MaterialRequestAlreadyExists = "MyERP:10003";

    // Inventory
    public const string InsufficientStock = "MyERP:05002";
}
