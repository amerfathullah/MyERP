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
}
