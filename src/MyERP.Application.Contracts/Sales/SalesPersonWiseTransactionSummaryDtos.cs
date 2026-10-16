using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetSalesPersonWiseTransactionSummaryInput
{
    public Guid CompanyId { get; set; }

    /// <summary>"Sales Invoice", "Sales Order", or "Delivery Note"</summary>
    public string DocType { get; set; } = "Sales Invoice";

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SalesPersonId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? TerritoryId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? ItemCode { get; set; }
    public bool ShowReturnEntries { get; set; }
}

public class SalesPersonWiseTransactionRowDto
{
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? TerritoryName { get; set; }
    public string? WarehouseName { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroup { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal BaseNetAmount { get; set; }
    public Guid SalesPersonId { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public decimal AllocatedPercentage { get; set; }
    public decimal ContributionQuantity { get; set; }
    public decimal ContributionAmount { get; set; }
}

public class SalesPersonWiseTransactionSummaryReportDto
{
    public string Currency { get; set; } = "MYR";
    public string DocType { get; set; } = "Sales Invoice";
    public List<SalesPersonWiseTransactionRowDto> Rows { get; set; } = new();
    public decimal TotalQuantity { get; set; }
    public decimal TotalNetAmount { get; set; }
    public decimal TotalContributionQuantity { get; set; }
    public decimal TotalContributionAmount { get; set; }
    public int TransactionCount { get; set; }
}
