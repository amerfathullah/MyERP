using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.BankAccountTypes.Default)]
public class BankAccountTypeAppService : MyERPAppService, IBankAccountTypeAppService
{
    private readonly IRepository<BankAccountType, Guid> _repository;

    public BankAccountTypeAppService(IRepository<BankAccountType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<BankAccountTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new BankAccountTypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<BankAccountTypeDto>> GetListAsync(GetBankAccountTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.AccountTypeName.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.AccountTypeName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new BankAccountTypeMapper().Map).ToList();
        return new PagedResultDto<BankAccountTypeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.BankAccountTypes.Create)]
    public async Task<BankAccountTypeDto> CreateAsync(CreateUpdateBankAccountTypeDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.AccountTypeName.ToLower() == input.AccountTypeName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Bank account type '{input.AccountTypeName}' already exists.");
        }

        var entity = new BankAccountType(
            GuidGenerator.Create(),
            input.AccountTypeName.Trim(),
            input.Description?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new BankAccountTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankAccountTypes.Edit)]
    public async Task<BankAccountTypeDto> UpdateAsync(Guid id, CreateUpdateBankAccountTypeDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.AccountTypeName.ToLower() == input.AccountTypeName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Bank account type '{input.AccountTypeName}' already exists.");
        }

        entity.SetAccountTypeName(input.AccountTypeName.Trim());
        entity.Description = input.Description?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new BankAccountTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankAccountTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
