using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class CashFlowRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Optional periodicity ("Monthly", "Quarterly", "Yearly"). When omitted, evaluates as a single range.
    /// </summary>
    public string? Periodicity { get; set; }

    /// <summary>
    /// When true, each period column accumulates values from the later of its fiscal year start date and FromDate
    /// (ERPNext commits 461d586d8b & 80ef213995).
    /// </summary>
    public bool AccumulatedValues { get; set; }

    public bool ShowOpeningAndClosingBalance { get; set; } = true;
}

public class CashFlowPeriodColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public decimal OperatingTotal { get; set; }
    public decimal InvestingTotal { get; set; }
    public decimal FinancingTotal { get; set; }
    public decimal NetCashChange { get; set; }
    public decimal OpeningCashBalance { get; set; }
    public decimal ClosingCashBalance { get; set; }
}

public class CashFlowStatementDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public List<CashFlowLineItem> OperatingActivities { get; set; } = new();
    public decimal OperatingTotal { get; set; }

    public List<CashFlowLineItem> InvestingActivities { get; set; } = new();
    public decimal InvestingTotal { get; set; }

    public List<CashFlowLineItem> FinancingActivities { get; set; } = new();
    public decimal FinancingTotal { get; set; }

    public decimal NetCashChange { get; set; }
    public decimal OpeningCashBalance { get; set; }
    public decimal ClosingCashBalance { get; set; }

    public List<CashFlowPeriodColumnDto> Periods { get; set; } = new();
}

public class CashFlowLineItem
{
    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }

    public CashFlowLineItem() { }

    public CashFlowLineItem(string label, decimal amount)
    {
        Label = label;
        Amount = amount;
    }
}
