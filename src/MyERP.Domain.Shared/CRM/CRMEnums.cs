namespace MyERP.CRM;

public enum LeadStatus
{
    New = 0,
    Open = 1,
    Replied = 2,
    Interested = 3,
    Qualified = 4,
    Converted = 5,
    Lost = 6,
    DoNotContact = 7,
}

public enum OpportunityStatus
{
    Open = 0,
    Replied = 1,
    Quotation = 2,
    Converted = 3,
    Lost = 4,
    Closed = 5,
}

public enum LeadSource
{
    Website = 0,
    Referral = 1,
    Campaign = 2,
    ColdCall = 3,
    Advertisement = 4,
    SocialMedia = 5,
    TradeShow = 6,
    Partner = 7,
    Other = 8,
}

public enum OpportunityType
{
    Sales = 0,
    Support = 1,
    Maintenance = 2,
}

public enum ContractStatus
{
    Unsigned = 0,
    Active = 1,
    InactiveByExpiry = 2,
    InactiveByAutoRenewFailure = 3,
    Cancelled = 4,
}

/// <summary>Per ERPNext Contract.fulfilment_status — derived from checklist completion, never set directly.</summary>
public enum ContractFulfilmentStatus
{
    NotApplicable = 0,
    Unfulfilled = 1,
    PartiallyFulfilled = 2,
    Fulfilled = 3,
    Lapsed = 4,
}

public enum ShipmentStatus
{
    Draft = 0,
    Booked = 1,
    InTransit = 2,
    Delivered = 3,
    Cancelled = 4,
}
