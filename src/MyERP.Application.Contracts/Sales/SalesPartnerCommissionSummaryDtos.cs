using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetSalesPartnerCommissionSummaryInput
{
    public Guid CompanyId { get; set; }

    /// <summary>"Sales Order", "Sales Invoice", or "POS Invoice".</summary>
    public string DocType { get; set; } = "Sales Invoice";

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SalesPartnerId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? TerritoryId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? Brand { get; set; }
    public bool ShowReturnEntries { get; set; }
}

public class SalesPartnerCommissionSummaryRowDto
{
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? TerritoryName { get; set; }
    public Guid SalesPartnerId { get; set; }
    public string SalesPartnerName { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalCommission { get; set; }
}

public class SalesPartnerCommissionSummaryReportDto
{
    public string Currency { get; set; } = "MYR";
    public string DocType { get; set; } = "Sales Invoice";
    public List<SalesPartnerCommissionSummaryRowDto> Rows { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalCommission { get; set; }
    public int TransactionCount { get; set; }
}

public class SalesPartnerTransactionSummaryRowDto
{
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? TerritoryName { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroupName { get; set; }
    public string? Brand { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid SalesPartnerId { get; set; }
    public string SalesPartnerName { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; }
    public decimal Commission { get; set; }
}

public class SalesPartnerTransactionSummaryReportDto
{
    public string Currency { get; set; } = "MYR";
    public string DocType { get; set; } = "Sales Invoice";
    public List<SalesPartnerTransactionSummaryRowDto> Rows { get; set; } = new();
    public decimal TotalQty { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalCommission { get; set; }
}
