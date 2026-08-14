using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using MyERP.HumanResources.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class ExpenseClaimAppService : ApplicationService, IExpenseClaimAppService
{
    private readonly IRepository<ExpenseClaim, Guid> _repository;
    public ExpenseClaimAppService(IRepository<ExpenseClaim, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ExpenseClaimDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
             query = query.Where(x => x.EmployeeName != null && x.EmployeeName.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(e => e.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ExpenseClaimDto>(totalCount, items.Select(x => ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(x)).ToList());
    }

    public async Task<ExpenseClaimDto> GetAsync(Guid id)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
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
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> ApproveAsync(Guid id)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);
        ec.Approve();
        await _repository.UpdateAsync(ec);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> SubmitAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Submit();
        await _repository.UpdateAsync(ec);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> RejectAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Reject();
        await _repository.UpdateAsync(ec);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ExpenseClaimDto> CancelAsync(Guid id)
    {
        var ec = await _repository.GetAsync(id);
        ec.Cancel();
        await _repository.UpdateAsync(ec);
        return ObjectMapper.Map<ExpenseClaim, ExpenseClaimDto>(ec);
    }

    /// <summary>
    /// Creates a Payment Entry to reimburse an approved/submitted expense claim.
    /// Per ERPNext: validates advance linkage to prevent double-payment.
    /// Per DO-NOT: "Allow expense claim GL posting without verifying advance linkage"
    /// Uses ExpenseClaimManager domain service for validation per DDD.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<Guid> ReimburseAsync(Guid id, Guid paidFromAccountId)
    {
        var ec = (await _repository.WithDetailsAsync()).First(e => e.Id == id);

        // Delegate validation to domain service
        var claimManager = LazyServiceProvider.LazyGetRequiredService<ExpenseClaimManager>();
        claimManager.ValidateForReimbursement(ec);

        // Calculate reimbursable via domain service (single source of truth)
        var reimbursableAmount = claimManager.CalculateReimbursableAmount(ec);

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
}

