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

[Authorize(MyERPPermissions.BankAccountSubtypes.Default)]
public class BankAccountSubtypeAppService : MyERPAppService, IBankAccountSubtypeAppService
{
    private readonly IRepository<BankAccountSubtype, Guid> _repository;

    public BankAccountSubtypeAppService(IRepository<BankAccountSubtype, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<BankAccountSubtypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new BankAccountSubtypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<BankAccountSubtypeDto>> GetListAsync(GetBankAccountSubtypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.AccountSubtypeName.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.AccountSubtypeName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new BankAccountSubtypeMapper().Map).ToList();
        return new PagedResultDto<BankAccountSubtypeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.BankAccountSubtypes.Create)]
    public async Task<BankAccountSubtypeDto> CreateAsync(CreateUpdateBankAccountSubtypeDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.AccountSubtypeName.ToLower() == input.AccountSubtypeName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Bank account subtype '{input.AccountSubtypeName}' already exists.");
        }

        var entity = new BankAccountSubtype(
            GuidGenerator.Create(),
            input.AccountSubtypeName.Trim(),
            input.Description?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new BankAccountSubtypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankAccountSubtypes.Edit)]
    public async Task<BankAccountSubtypeDto> UpdateAsync(Guid id, CreateUpdateBankAccountSubtypeDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.AccountSubtypeName.ToLower() == input.AccountSubtypeName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Bank account subtype '{input.AccountSubtypeName}' already exists.");
        }

        entity.SetAccountSubtypeName(input.AccountSubtypeName.Trim());
        entity.Description = input.Description?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new BankAccountSubtypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BankAccountSubtypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
