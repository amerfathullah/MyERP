namespace MyERP.Notification;

public static class NotificationConsts
{
    public const int MaxSubjectLength = 256;
    public const int MaxBodyLength = 4096;
    public const int MaxRecipientEmailLength = 256;
}

public enum NotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}
