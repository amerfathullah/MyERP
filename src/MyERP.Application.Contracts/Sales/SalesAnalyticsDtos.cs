using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public enum AnalyticsGroupBy { Customer = 0, Item = 1, Territory = 2, SalesPerson = 3, ItemGroup = 4 }
public enum AnalyticsPeriodType { Monthly = 0, Quarterly = 1, Yearly = 2 }

public class SalesAnalyticsRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public AnalyticsGroupBy GroupBy { get; set; }
    public AnalyticsPeriodType PeriodType { get; set; }
    public string? ValueField { get; set; }
    public List<string>? EntityIds { get; set; }
}

public class SalesAnalyticsReportDto
{
    public List<string> PeriodLabels { get; set; } = new();
    public List<SalesAnalyticsRowDto> Rows { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public List<decimal> PeriodTotals { get; set; } = new();
}

public class SalesAnalyticsRowDto
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public List<decimal> PeriodValues { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Growth { get; set; }
}

public class CustomerCreditBalanceInputDto
{
    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class CustomerCreditBalanceDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal CreditBalance { get; set; }
    public bool BypassCreditLimitCheckAtSalesOrder { get; set; }
    public bool IsFrozen { get; set; }
    public bool Disabled { get; set; }
}

public class CustomersWithoutSalesInputDto
{
    public Guid? CompanyId { get; set; }
}

public class CustomerWithoutSalesDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerGroup { get; set; }
    public string? Territory { get; set; }
}

