namespace MyERP.Inventory;

public enum QualityActionType
{
    Corrective = 0,
    Preventive = 1,
}

public enum QualityActionStatus
{
    Open = 0,
    Resolved = 1,
    Closed = 2,
}

public enum QualityReviewStatus
{
    Open = 0,
    Passed = 1,
    Failed = 2,
}

public enum NonConformanceStatus
{
    Open = 0,
    Resolved = 1,
    Cancelled = 2,
}

public enum QualityMeetingStatus
{
    Open = 0,
    Closed = 1,
}

public enum QualityFeedbackDocumentType
{
    User = 0,
    Customer = 1,
}
