namespace MyERP.Support;

/// <summary>Issue lifecycle status.</summary>
public enum IssueStatus
{
    Open = 0,
    Replied = 1,
    OnHold = 2,
    Closed = 3,
    Cancelled = 4,
}

/// <summary>
/// SLA agreement status on an Issue — mirrors ERPNext's Issue.agreement_status.
/// Driven by ServiceLevelAgreement deadlines and the Issue's own state transitions.
/// </summary>
public enum AgreementStatus
{
    FirstResponseDue = 0,
    ResolutionDue = 1,
    Fulfilled = 2,
    Failed = 3,
    Paused = 4,
}

public static class IssueConsts
{
    public const int MaxSubjectLength = 500;
    public const int MaxDescriptionLength = 4000;
    public const int MaxResolutionLength = 4000;
    public const int MaxPriorityLength = 20;
    public const int MaxIssueTypeLength = 100;
    public const int MaxRaisedViaLength = 50;
}

public static class ServiceLevelAgreementConsts
{
    public const int MaxNameLength = 100;
    public const int MaxEntityTypeLength = 50;
}

public static class ServiceLevelPriorityConsts
{
    public const int MaxPriorityNameLength = 50;
}

public static class IssuePriorityConsts
{
    public const int MaxNameLength = 50;
    public const int MaxDescriptionLength = 500;
}

public static class IssueTypeConsts
{
    public const int MaxNameLength = 50;
    public const int MaxDescriptionLength = 500;
}
