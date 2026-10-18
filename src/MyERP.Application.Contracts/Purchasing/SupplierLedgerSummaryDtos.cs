using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Purchasing;

public class SupplierLedgerSummaryRequestDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public Guid? SupplierId { get; set; }

    public Guid? SupplierGroupId { get; set; }
}

public class SupplierLedgerSummaryRowDto
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierGroupName { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal ClosingBalance { get; set; }
    public string DrOrCr { get; set; } = string.Empty;
    public string Currency { get; set; } = "MYR";
}

public class SupplierLedgerSummaryReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<SupplierLedgerSummaryRowDto> Rows { get; set; } = new();
    public SupplierLedgerSummaryRowDto TotalRow { get; set; } = new();
}
