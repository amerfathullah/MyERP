namespace MyERP.Projects;

public enum ProjectStatus
{
    Open = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum ProjectTaskStatus
{
    Open = 0,
    Working = 1,
    PendingReview = 2,
    Overdue = 3,
    Completed = 4,
    Cancelled = 5,
}

public enum ProjectPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3,
}

public enum PercentCompleteMethod
{
    Manual = 0,
    TaskCompletion = 1,
    TaskProgress = 2,
    TaskWeight = 3,
}
