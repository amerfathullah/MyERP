using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class EvaluateRulesDto
{
    public Guid CompanyId { get; set; }
    public bool ForceReEvaluate { get; set; }
}

public class BankTransactionRuleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string RuleName { get; set; } = null!;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public int TransactionType { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public int ClassifyAs { get; set; }
    public string? DescriptionContains { get; set; }
}

public class CreateBankTransactionRuleDto
{
    public Guid CompanyId { get; set; }
    public string RuleName { get; set; } = null!;
    public int TransactionType { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public int ClassifyAs { get; set; }
    public string? DescriptionContains { get; set; }
}
