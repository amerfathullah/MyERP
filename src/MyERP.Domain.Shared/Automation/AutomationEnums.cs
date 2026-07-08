namespace MyERP.Automation;

/// <summary>
/// Events that can trigger an automation rule.
/// Mapped from ERPNext hooks (on_submit, on_cancel, etc.)
/// </summary>
public enum AutomationTrigger
{
    DocumentSubmitted = 0,
    DocumentApproved = 1,
    DocumentPosted = 2,
    DocumentCancelled = 3,
    PaymentReceived = 4,
    StockBelowReorder = 5,
    InvoiceOverdue = 6,
    EInvoiceValidated = 7,
    EInvoiceRejected = 8,
    ApprovalRequired = 9,
    ScheduledDaily = 100,
    ScheduledWeekly = 101,
    ScheduledMonthly = 102,
}

/// <summary>
/// Actions that an automation rule can execute.
/// </summary>
public enum AutomationAction
{
    SendNotification = 0,
    SendEmail = 1,
    SubmitToLhdn = 2,
    CreateApprovalRequest = 3,
    UpdateField = 4,
    CreateFollowUpTask = 5,
    PostToAccounting = 6,
}

public static class AutomationRuleConsts
{
    public const int MaxNameLength = 128;
    public const int MaxDescriptionLength = 512;
    public const int MaxDocumentTypeLength = 64;
    public const int MaxConditionExpressionLength = 1024;
    public const int MaxActionConfigLength = 2048;
}
