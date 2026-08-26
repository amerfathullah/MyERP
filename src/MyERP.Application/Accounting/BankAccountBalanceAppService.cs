using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.BankAccountBalances.Default)]
public class BankAccountBalanceAppService : MyERPAppService, IBankAccountBalanceAppService
{
    private readonly IRepository<BankAccountBalance, Guid> _repository;
    private readonly IRepository<BankAccount, Guid> _bankAccountRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public BankAccountBalanceAppService(
        IRepository<BankAccountBalance, Guid> repository,
        IRepository<BankAccount, Guid> bankAccountRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _repository = repository;
        _bankAccountRepository = bankAccountRepository;
        _companyRepository = companyRepository;
    }

    public async Task<BankAccountBalanceDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new BankAccountBalanceMapper().Map(entity);
        await FillLookupsAsync(new[] { dto });
        return dto;
    }

    public async Task<PagedResultDto<BankAccountBalanceDto>> GetListAsync(GetBankAccountBalanceListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.BankAccountId.HasValue)
            query = query.Where(x => x.BankAccountId == input.BankAccountId.Value);
        if (input.FromDate.HasValue)
            query = query.Where(x => x.Date >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(x => x.Date <= input.ToDate.Value);

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.Date)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new BankAccountBalanceMapper().Map(e)).ToList();
        await FillLookupsAsync(dtos);
        return new PagedResultDto<BankAccountBalanceDto>(totalCount, dtos);
    }

    public async Task<List<BankAccountBalanceDto>> GetAllListAsync(Guid bankAccountId)
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(
            query.Where(x => x.BankAccountId == bankAccountId).OrderByDescending(x => x.Date));

        var dtos = entities.Select(e => new BankAccountBalanceMapper().Map(e)).ToList();
        await FillLookupsAsync(dtos);
        return dtos;
    }

    [Authorize(MyERPPermissions.BankAccountBalances.Create)]
    public async Task<BankAccountBalanceDto> CreateAsync(CreateUpdateBankAccountBalanceDto input)
    {
        var entity = new BankAccountBalance(GuidGenerator.Create(), input.BankAccountId, input.Date, input.Balance, CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        var dto = new BankAccountBalanceMapper().Map(entity);
        await FillLookupsAsync(new[] { dto });
        return dto;
    }

    [Authorize(MyERPPermissions.BankAccountBalances.Edit)]
    public async Task<BankAccountBalanceDto> UpdateAsync(Guid id, CreateUpdateBankAccountBalanceDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.BankAccountId = input.BankAccountId;
        entity.Date = input.Date;
        entity.Balance = input.Balance;

        await _repository.UpdateAsync(entity);
        var dto = new BankAccountBalanceMapper().Map(entity);
        await FillLookupsAsync(new[] { dto });
        return dto;
    }

    [Authorize(MyERPPermissions.BankAccountBalances.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task FillLookupsAsync(IReadOnlyCollection<BankAccountBalanceDto> dtos)
    {
        var bankAccountIds = dtos.Select(d => d.BankAccountId).Distinct().ToList();
        if (bankAccountIds.Count == 0)
            return;

        var bankAccounts = (await _bankAccountRepository.GetQueryableAsync())
            .Where(b => bankAccountIds.Contains(b.Id))
            .ToDictionary(b => b.Id, b => b);

        var companyIds = bankAccounts.Values.Select(b => b.CompanyId).Distinct().ToList();
        var companies = (await _companyRepository.GetQueryableAsync())
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        foreach (var dto in dtos)
        {
            if (!bankAccounts.TryGetValue(dto.BankAccountId, out var bankAccount))
                continue;

            dto.BankAccountName = bankAccount.AccountName;
            dto.CompanyId = bankAccount.CompanyId;
            if (companies.TryGetValue(bankAccount.CompanyId, out var companyName))
                dto.CompanyName = companyName;
        }
    }
}
