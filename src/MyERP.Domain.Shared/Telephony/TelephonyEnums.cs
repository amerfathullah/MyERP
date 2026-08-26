namespace MyERP.Telephony;

public enum CallDirection
{
    Incoming = 0,
    Outgoing = 1
}

public enum CallStatus
{
    Ringing = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Busy = 4,
    NoAnswer = 5,
    Queued = 6,
    Cancelled = 7
}

public enum CallRoutingMode
{
    Sequential = 0,
    Simultaneous = 1
}
