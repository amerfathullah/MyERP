using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class PeriodClosingVoucherDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid ClosingAccountId { get; set; }
    public string? ClosingAccountName { get; set; }
    public decimal TotalClosingAmount { get; set; }
    public decimal? StockValueDifference { get; set; }
    public int Status { get; set; }
    public string? Remarks { get; set; }
    public int EntryCount { get; set; }
}

public class SubmitPeriodClosingVoucherDto
{
    public decimal? StockValueDifference { get; set; }
}

public class PeriodClosingStockDifferenceDto
{
    public decimal AccountBalance { get; set; }
    public decimal StockValue { get; set; }
    public decimal Difference { get; set; }
    public decimal Tolerance { get; set; } = 1.0m;
    public bool WithinTolerance { get; set; }
    public bool HasStockTransactions { get; set; }
}

public class PcvGlEntryDto
{
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CostCenterId { get; set; }
    public DateTime PostingDate { get; set; }
}

public class CreatePeriodClosingVoucherDto
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid ClosingAccountId { get; set; }
    public string? Remarks { get; set; }
}
