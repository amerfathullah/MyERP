using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ExchangeRateRevaluationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal TotalGainLoss { get; set; }
    public int EntryCount { get; set; }
}

public class EligibleAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public string AccountCurrency { get; set; } = null!;
    public decimal BalanceInAccountCurrency { get; set; }
    public decimal CurrentExchangeRate { get; set; }
    public decimal BalanceInCompanyCurrency { get; set; }
    public decimal GainLoss { get; set; }
}

public class CreateRevaluationDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal RoundingLossAllowance { get; set; } = 0.05m;
}
