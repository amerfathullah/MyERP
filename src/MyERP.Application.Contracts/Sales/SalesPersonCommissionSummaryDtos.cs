using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetSalesPersonCommissionSummaryInput
{
    public Guid CompanyId { get; set; }

    /// <summary>"Sales Invoice" or "Sales Order"</summary>
    public string DocType { get; set; } = "Sales Invoice";

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SalesPersonId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? TerritoryId { get; set; }
}

public class SalesPersonCommissionSummaryRowDto
{
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? TerritoryName { get; set; }
    public decimal NetAmount { get; set; }
    public Guid SalesPersonId { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public decimal AllocatedPercentage { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal Incentives { get; set; }
}

public class SalesPersonCommissionSummaryReportDto
{
    public string Currency { get; set; } = "MYR";
    public string DocType { get; set; } = "Sales Invoice";
    public List<SalesPersonCommissionSummaryRowDto> Rows { get; set; } = new();
    public decimal TotalNetAmount { get; set; }
    public decimal TotalAllocatedAmount { get; set; }
    public decimal TotalIncentives { get; set; }
    public int TransactionCount { get; set; }
}
