using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class SalesPaymentSummaryFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? PosProfileId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class SalesPaymentSummaryRowDto
{
    public DateTime PostingDate { get; set; }
    public string Cashier { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string CostCenter { get; set; } = string.Empty;
    public string ModeOfPayment { get; set; } = string.Empty;
    public decimal NetTotal { get; set; }
    public decimal TotalTaxes { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class SalesPaymentSummaryReportDto
{
    public List<SalesPaymentSummaryRowDto> Rows { get; set; } = new();
    public decimal TotalNet { get; set; }
    public decimal TotalTaxes { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
}
