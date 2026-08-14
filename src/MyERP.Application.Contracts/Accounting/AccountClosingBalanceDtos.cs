using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class AccountClosingBalanceDto : EntityDto<Guid>
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public string? AccountCode { get; set; }
    public DateTime ClosingDate { get; set; }
    public string Period { get; set; } = null!;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? CostCenterName { get; set; }
    public string? FinanceBook { get; set; }
}

public class RebuildClosingBalanceDto
{
    public Guid CompanyId { get; set; }
    public DateTime ClosingDate { get; set; }
    public string Period { get; set; } = null!;
}

public class ClosingBalanceStatusDto
{
    public string? LatestPeriod { get; set; }
    public DateTime? LatestClosingDate { get; set; }
    public int TotalBalances { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
}
