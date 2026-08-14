using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class GeneralLedgerLineDto
{
    public Guid Id { get; set; }
    public DateTime PostingDate { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public string? VoucherNumber { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal Balance { get; set; }
    public string? PartyType { get; set; }
    public string? PartyName { get; set; }
    public string? CostCenterName { get; set; }
    public string? Description { get; set; }
}

public class GeneralLedgerReportDto
{
    public List<GeneralLedgerLineDto> Entries { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
    public int Count { get; set; }
}

public class GeneralLedgerFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? AccountId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public string? VoucherNumber { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class VoucherLedgerEntryDto
{
    public DateTime PostingDate { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? CostCenterName { get; set; }
    public string? Description { get; set; }
    public string? FinanceBook { get; set; }
}

public class VoucherLedgerDto
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public string? VoucherNumber { get; set; }
    public List<VoucherLedgerEntryDto> Entries { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced => Math.Abs(TotalDebit - TotalCredit) < 0.01m;
}
