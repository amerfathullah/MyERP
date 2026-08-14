using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class ExpenseClaimDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ExpenseType { get; set; }
    public decimal TotalClaimedAmount { get; set; }
    public decimal TotalSanctionedAmount { get; set; }
    public decimal TotalAmountReimbursed { get; set; }
    public int Status { get; set; }
    public ExpenseClaimDetailDto[] Expenses { get; set; } = [];
}

public class ExpenseClaimDetailDto
{
    public Guid Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class CreateExpenseClaimDto
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ExpenseType { get; set; }
    public CreateExpenseDetailDto[] Expenses { get; set; } = [];
}

public class CreateExpenseDetailDto
{
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
}
