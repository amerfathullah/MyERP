using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class AccountClosingBalanceAppService : ApplicationService, IAccountClosingBalanceAppService
{
    private readonly AccountClosingBalanceService _closingBalanceService;
    private readonly IRepository<AccountClosingBalance, Guid> _repository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Accounting.Entities.CostCenter, Guid> _costCenterRepository;

    public AccountClosingBalanceAppService(
        AccountClosingBalanceService closingBalanceService,
        IRepository<AccountClosingBalance, Guid> repository,
        IRepository<Account, Guid> accountRepository,
        IRepository<Accounting.Entities.CostCenter, Guid> costCenterRepository)
    {
        _closingBalanceService = closingBalanceService;
        _repository = repository;
        _accountRepository = accountRepository;
        _costCenterRepository = costCenterRepository;
    }

    /// <summary>
    /// Gets all closing balances for a company at a specific period.
    /// Used by Trial Balance, Balance Sheet for O(1) reporting.
    /// </summary>
    public async Task<List<AccountClosingBalanceDto>> GetListAsync(Guid companyId, string period)
    {
        var balances = await _closingBalanceService.GetAllBalancesAsync(companyId, period);
        if (!balances.Any())
            return [];

        // Batch-resolve account names
        var accountIds = balances.Select(b => b.AccountId).Distinct().ToList();
        var accounts = (await _accountRepository.GetQueryableAsync())
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.AccountName, a.AccountCode })
            .ToList()
            .ToDictionary(a => a.Id);

        // Batch-resolve cost center names
        var ccIds = balances.Where(b => b.CostCenterId.HasValue)
            .Select(b => b.CostCenterId!.Value).Distinct().ToList();
        var costCenters = ccIds.Any()
            ? (await _costCenterRepository.GetQueryableAsync())
                .Where(cc => ccIds.Contains(cc.Id))
                .Select(cc => new { cc.Id, cc.Name })
                .ToList()
                .ToDictionary(cc => cc.Id, cc => cc.Name)
            : new Dictionary<Guid, string>();

        return balances.Select(b =>
        {
            accounts.TryGetValue(b.AccountId, out var acct);
            costCenters.TryGetValue(b.CostCenterId ?? Guid.Empty, out var ccName);
            return new AccountClosingBalanceDto
            {
                Id = b.Id,
                AccountId = b.AccountId,
                AccountName = acct?.AccountName ?? "Unknown",
                AccountCode = acct?.AccountCode,
                ClosingDate = b.ClosingDate,
                Period = b.Period,
                Debit = b.Debit,
                Credit = b.Credit,
                Balance = b.Balance,
                CostCenterId = b.CostCenterId,
                CostCenterName = ccName,
                FinanceBook = b.FinanceBook
            };
        }).OrderBy(b => b.AccountCode).ToList();
    }

    /// <summary>
    /// Gets the current closing balance status for a company (dashboard widget).
    /// </summary>
    public async Task<ClosingBalanceStatusDto> GetStatusAsync(Guid companyId)
    {
        var latest = await _closingBalanceService.GetLatestClosingAsync(companyId);
        if (latest == null)
            return new ClosingBalanceStatusDto();

        var balances = await _closingBalanceService.GetAllBalancesAsync(companyId, latest.Period);
        var totalDebit = balances.Sum(b => b.Debit);
        var totalCredit = balances.Sum(b => b.Credit);

        return new ClosingBalanceStatusDto
        {
            LatestPeriod = latest.Period,
            LatestClosingDate = latest.ClosingDate,
            TotalBalances = balances.Count,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m
        };
    }

    /// <summary>
    /// Manually rebuilds closing balances for a company at a given period.
    /// Called after GL repost, manual corrections, or admin maintenance.
    /// </summary>
    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<int> RebuildAsync(RebuildClosingBalanceDto input)
    {
        return await _closingBalanceService.RebuildAsync(
            input.CompanyId, input.ClosingDate, input.Period, CurrentTenant.Id);
    }
}
