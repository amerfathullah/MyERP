using System;
using MyERP.Accounting;
using MyERP.Core;
using Volo.Abp.Application.Dtos;

namespace MyERP.Dtos;

public class BudgetDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string BudgetAgainst { get; set; } = null!;
    public Guid BudgetAgainstId { get; set; }
    public string? BudgetAgainstName { get; set; }
    public DocumentStatus Status { get; set; }
    public BudgetAction ActionIfAnnualBudgetExceeded { get; set; }
    public BudgetAction ActionIfAccumulatedMonthlyBudgetExceeded { get; set; }
    public BudgetAccountDto[] Accounts { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class BudgetAccountDto : EntityDto<Guid>
{
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal BudgetAmount { get; set; }
}

public class CreateBudgetDto
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string BudgetAgainst { get; set; } = null!;
    public Guid BudgetAgainstId { get; set; }
    public string? BudgetAgainstName { get; set; }
    public BudgetAction ActionIfAnnualBudgetExceeded { get; set; } = BudgetAction.Stop;
    public BudgetAction ActionIfAccumulatedMonthlyBudgetExceeded { get; set; } = BudgetAction.Warn;
    public CreateBudgetAccountDto[] Accounts { get; set; } = [];
}

public class CreateBudgetAccountDto
{
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal BudgetAmount { get; set; }
}

public class GetBudgetListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public string? Filter { get; set; }
    public string? Status { get; set; }
}
