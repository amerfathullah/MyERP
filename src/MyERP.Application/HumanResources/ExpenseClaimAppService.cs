using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

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

[Authorize(MyERPPermissions.Employees.Default)]
public class ExpenseClaimAppService : ApplicationService
{
    private readonly IRepository<ExpenseClaim, Guid> _repository;
    public ExpenseClaimAppService(IRepository<ExpenseClaim, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ExpenseClaimDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(e => e.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ExpenseClaimDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<ExpenseClaimDto> GetAsync(Guid id)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);
        return MapToDto(ec);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<ExpenseClaimDto> CreateAsync(CreateExpenseClaimDto input)
    {
        var ec = new ExpenseClaim(GuidGenerator.Create(), input.CompanyId, input.EmployeeId,
            input.PostingDate, CurrentTenant.Id)
        { EmployeeName = input.EmployeeName, ExpenseType = input.ExpenseType };
        foreach (var e in input.Expenses)
            ec.AddExpense(e.ExpenseDate, e.Description, e.Amount);
        await _repository.InsertAsync(ec);
        return MapToDto(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> ApproveAsync(Guid id)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);
        ec.Approve();
        await _repository.UpdateAsync(ec);
        return MapToDto(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> SubmitAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Submit();
        await _repository.UpdateAsync(ec);
        return MapToDto(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> RejectAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Reject();
        await _repository.UpdateAsync(ec);
        return MapToDto(ec);
    }

    /// <summary>
    /// Creates a Payment Entry to reimburse an approved/submitted expense claim.
    /// Per ERPNext: validates advance linkage to prevent double-payment.
    /// Per DO-NOT: "Allow expense claim GL posting without verifying advance linkage"
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<Guid> ReimburseAsync(Guid id, Guid paidFromAccountId)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);

        if (ec.Status != DocumentStatus.Submitted)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "ExpenseClaim")
                .WithData("status", ec.Status.ToString());

        // Calculate reimbursable amount (claimed minus any advance already paid)
        var reimbursableAmount = ec.TotalClaimedAmount - ec.AdvanceAmount - ec.TotalAmountReimbursed;
        if (reimbursableAmount <= 0)
            throw new Volo.Abp.BusinessException("MyERP:14003")
                .WithData("reason", "No amount pending reimbursement");

        // Create Payment Entry (company pays employee)
        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentEntry, Guid>>();
        var paymentNumber = await numberGenerator.GenerateAsync("PaymentEntry", ec.CompanyId);

        var pe = new MyERP.Accounting.Entities.PaymentEntry(
            GuidGenerator.Create(),
            ec.CompanyId,
            MyERP.Accounting.PaymentType.Pay,
            DateTime.UtcNow.Date,
            reimbursableAmount,
            paidFromAccountId,
            ec.PayableAccountId ?? paidFromAccountId);

        pe.PaymentNumber = paymentNumber;
        pe.PartyType = "Employee";
        pe.PartyId = ec.EmployeeId;
        pe.Notes = $"Reimbursement for expense claim {ec.ExpenseType} (Posted: {ec.PostingDate:yyyy-MM-dd})";

        await peRepo.InsertAsync(pe, autoSave: true);

        // Update expense claim with reimbursement amount
        ec.TotalAmountReimbursed += reimbursableAmount;
        await _repository.UpdateAsync(ec, autoSave: true);

        return pe.Id;
    }

    private static ExpenseClaimDto MapToDto(ExpenseClaim e) => new()
    {
        Id = e.Id, CompanyId = e.CompanyId, EmployeeId = e.EmployeeId,
        EmployeeName = e.EmployeeName, PostingDate = e.PostingDate,
        ExpenseType = e.ExpenseType, TotalClaimedAmount = e.TotalClaimedAmount,
        TotalSanctionedAmount = e.TotalSanctionedAmount,
        TotalAmountReimbursed = e.TotalAmountReimbursed, Status = (int)e.Status,
        Expenses = e.Expenses.Select(d => new ExpenseClaimDetailDto
        {
            Id = d.Id, ExpenseDate = d.ExpenseDate, Description = d.Description, Amount = d.Amount,
        }).ToArray(),
    };
}
