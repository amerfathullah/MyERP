using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class CashFlowRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
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
