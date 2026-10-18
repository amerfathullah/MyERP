using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Sales;

public class CustomerLedgerSummaryRequestDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? CustomerGroupId { get; set; }

    public Guid? TerritoryId { get; set; }
}

public class CustomerLedgerSummaryRowDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerGroupName { get; set; }
    public string? TerritoryName { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal ClosingBalance { get; set; }
    public string DrOrCr { get; set; } = string.Empty;
    public string Currency { get; set; } = "MYR";
}

public class CustomerLedgerSummaryReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<CustomerLedgerSummaryRowDto> Rows { get; set; } = new();
    public CustomerLedgerSummaryRowDto TotalRow { get; set; } = new();
}
