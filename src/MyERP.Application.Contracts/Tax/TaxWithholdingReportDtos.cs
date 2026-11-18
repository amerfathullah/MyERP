using System;
using System.Collections.Generic;

namespace MyERP.Tax;

public class TaxWithholdingReportFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Optional: "Supplier" or "Customer".</summary>
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public string? TaxCategory { get; set; }
}

public class TaxWithholdingDetailRowDto
{
    public Guid EntryId { get; set; }
    public string PartyType { get; set; } = "Supplier";
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string? Tin { get; set; }
    public string TaxCategory { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public bool HasLdc { get; set; }
    public string? CertificateNumber { get; set; }
}

public class TaxWithholdingDetailsReportDto
{
    public List<TaxWithholdingDetailRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalTaxableAmount { get; set; }
    public decimal TotalWithheldAmount { get; set; }
}

public class TdsComputationSummaryRowDto
{
    public string PartyType { get; set; } = "Supplier";
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string? Tin { get; set; }
    public string TaxCategory { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public bool HasLdc { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalTaxableAmount { get; set; }
    public decimal TotalWithheldAmount { get; set; }
}

public class TdsComputationSummaryReportDto
{
    public string? FiscalYearName { get; set; }
    public List<TdsComputationSummaryRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalTaxableAmount { get; set; }
    public decimal TotalWithheldAmount { get; set; }
}
