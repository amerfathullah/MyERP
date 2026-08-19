namespace MyERP.CRM;

public static class CompetitorConsts
{
    public const int MaxNameLength = 200;
    public const int MaxWebsiteLength = 500;
}

public static class MarketSegmentConsts
{
    public const int MaxNameLength = 100;
}

public static class IndustryTypeConsts
{
    public const int MaxNameLength = 200;
}

public static class SalesStageConsts
{
    public const int MaxStageNameLength = 100;
}

public static class CrmNoteConsts
{
    public const int MaxParentTypeLength = 50;
    public const int MaxNoteTextLength = 4000;
}

public static class CampaignConsts
{
    public const int MaxCampaignNameLength = 200;
    public const int MaxDescriptionLength = 2000;
}

/// <summary>Recipient type an Email Campaign targets. Maps to ERPNext's email_campaign_for.</summary>
public enum EmailCampaignFor
{
    Lead = 0,
    Contact = 1,
}

/// <summary>Email Campaign lifecycle — computed from Campaign schedule dates and unsubscribe events.</summary>
public enum EmailCampaignStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Unsubscribed = 3,
}

public static class EmailCampaignConsts
{
    public const int MaxSenderLength = 256;
}

/// <summary>What happens to an Appointment left Unverified past its verification window.</summary>
public enum ExpiredAppointmentAction
{
    NoAction = 0,
    CancelAppointment = 1,
}

public static class AppointmentBookingSettingsConsts
{
    public const int MinVerificationLinkExpiryMinutes = 15;
    public const int MaxVerificationLinkExpiryMinutes = 60;
}

/// <summary>Appointment lifecycle. Portal-created appointments start Unverified until the requester confirms by email.</summary>
public enum AppointmentStatus
{
    Unverified = 0,
    Open = 1,
    Closed = 2,
}

public static class AppointmentConsts
{
    public const int MaxCustomerNameLength = 200;
    public const int MaxPhoneLength = 30;
    public const int MaxEmailLength = 256;
    public const int MaxDetailsLength = 2000;
}

public static class ContractTemplateConsts
{
    public const int MaxTitleLength = 200;
    public const int MaxFulfilmentTermLength = 1000;
}
