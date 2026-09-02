namespace MyERP.Manufacturing;

public enum WorkOrderStatus
{
    Draft = 0,
    Submitted = 1,
    NotStarted = 2,
    InProcess = 3,
    Completed = 4,
    Stopped = 5,
    Cancelled = 6,
    Closed = 7,
}

/// <summary>Demand-generation period for Sales Forecast. Maps to ERPNext frequency.</summary>
public enum SalesForecastFrequency
{
    Weekly = 0,
    Monthly = 1,
}

/// <summary>Business status of a Sales Forecast, independent of the submit/cancel docstatus.</summary>
public enum SalesForecastStatus
{
    Planned = 0,
    MpsGenerated = 1,
    Cancelled = 2,
}
