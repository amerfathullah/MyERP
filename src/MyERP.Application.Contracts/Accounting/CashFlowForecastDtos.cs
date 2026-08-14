using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class CashFlowForecastRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public int ForecastDays { get; set; } = 90;
}

public class CashFlowForecastDto
{
    public DateTime AsOfDate { get; set; }
    public int ForecastDays { get; set; }
    public decimal CurrentCashBalance { get; set; }
    public decimal TotalExpectedInflows { get; set; }
    public decimal TotalExpectedOutflows { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal ProjectedClosingBalance { get; set; }
    public List<CashFlowForecastPeriodDto> Periods { get; set; } = [];
    public List<CashFlowForecastEntryDto> UpcomingInflows { get; set; } = [];
    public List<CashFlowForecastEntryDto> UpcomingOutflows { get; set; } = [];
    public CashFlowForecastSummaryDto Summary { get; set; } = new();
}

public class CashFlowForecastPeriodDto
{
    public string Label { get; set; } = null!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Inflows { get; set; }
    public decimal Outflows { get; set; }
    public decimal NetFlow { get; set; }
    public decimal CumulativeBalance { get; set; }
}

public class CashFlowForecastEntryDto
{
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public string PartyName { get; set; } = null!;
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public int DaysUntilDue { get; set; }
    public bool IsOverdue { get; set; }
}

public class CashFlowForecastSummaryDto
{
    public int OverdueReceivablesCount { get; set; }
    public decimal OverdueReceivablesAmount { get; set; }
    public int OverduePayablesCount { get; set; }
    public decimal OverduePayablesAmount { get; set; }
    public decimal CashRunwayDays { get; set; }
    public DateTime? ProjectedCashCrunchDate { get; set; }
}
