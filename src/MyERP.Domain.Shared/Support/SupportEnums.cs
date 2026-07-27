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

/// <summary>Issue priority levels.</summary>
public enum IssuePriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
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
