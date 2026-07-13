using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Dtos;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Budgets.Default)]
public class BudgetAppService : ApplicationService
{
    private readonly IRepository<Budget, Guid> _repository;

    public BudgetAppService(IRepository<Budget, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<BudgetDto>> GetListAsync(GetBudgetListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);
        if (input.FiscalYearId.HasValue)
            query = query.Where(b => b.FiscalYearId == input.FiscalYearId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(b => (b.BudgetAgainstName ?? "").ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<BudgetDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<BudgetDto> GetAsync(Guid id)
    {
        var budget = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        return MapToDto(budget);
    }

    [Authorize(MyERPPermissions.Budgets.Create)]
    public async Task<BudgetDto> CreateAsync(CreateBudgetDto input)
    {
        var budget = new Budget(GuidGenerator.Create(), input.CompanyId, input.FiscalYearId,
            input.BudgetAgainst, input.BudgetAgainstId, CurrentTenant.Id)
        {
            BudgetAgainstName = input.BudgetAgainstName,
            ActionIfAnnualBudgetExceeded = input.ActionIfAnnualBudgetExceeded,
            ActionIfAccumulatedMonthlyBudgetExceeded = input.ActionIfAccumulatedMonthlyBudgetExceeded,
        };

        foreach (var acc in input.Accounts)
            budget.AddAccount(acc.AccountId, acc.BudgetAmount, acc.AccountName);

        await _repository.InsertAsync(budget);
        return MapToDto(budget);
    }

    [Authorize(MyERPPermissions.Budgets.Submit)]
    public async Task<BudgetDto> SubmitAsync(Guid id)
    {
        var budget = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        budget.Submit();
        await _repository.UpdateAsync(budget);
        return MapToDto(budget);
    }

    [Authorize(MyERPPermissions.Budgets.Cancel)]
    public async Task<BudgetDto> CancelAsync(Guid id)
    {
        var budget = await _repository.GetAsync(id);
        budget.Cancel();
        await _repository.UpdateAsync(budget);
        return MapToDto(budget);
    }

    private static BudgetDto MapToDto(Budget b) => new()
    {
        Id = b.Id,
        CompanyId = b.CompanyId,
        FiscalYearId = b.FiscalYearId,
        BudgetAgainst = b.BudgetAgainst,
        BudgetAgainstId = b.BudgetAgainstId,
        BudgetAgainstName = b.BudgetAgainstName,
        Status = b.Status,
        ActionIfAnnualBudgetExceeded = b.ActionIfAnnualBudgetExceeded,
        ActionIfAccumulatedMonthlyBudgetExceeded = b.ActionIfAccumulatedMonthlyBudgetExceeded,
        Accounts = b.Accounts.Select(a => new BudgetAccountDto
        {
            Id = a.Id, AccountId = a.AccountId,
            AccountName = a.AccountName, BudgetAmount = a.BudgetAmount,
        }).ToArray(),
        CreationTime = b.CreationTime,
    };
}
