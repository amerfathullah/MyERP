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

[Authorize(MyERPPermissions.Banks.Default)]
public class BankAppService : MyERPAppService, IBankAppService
{
    private readonly IRepository<Bank, Guid> _repository;

    public BankAppService(IRepository<Bank, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<BankDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new BankMapper().Map(entity);
    }

    public async Task<PagedResultDto<BankDto>> GetListAsync(GetBankListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.BankName.ToLower().Contains(filter) ||
                                     (x.SwiftNumber != null && x.SwiftNumber.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.BankName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new BankMapper().Map).ToList();
        return new PagedResultDto<BankDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Banks.Create)]
    public async Task<BankDto> CreateAsync(CreateUpdateBankDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.BankName.ToLower() == input.BankName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Bank '{input.BankName}' already exists.");
        }

        var entity = new Bank(
            GuidGenerator.Create(),
            input.BankName.Trim(),
            input.SwiftNumber?.Trim(),
            input.Website?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new BankMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Banks.Edit)]
    public async Task<BankDto> UpdateAsync(Guid id, CreateUpdateBankDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.BankName.ToLower() == input.BankName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Bank '{input.BankName}' already exists.");
        }

        entity.SetBankName(input.BankName.Trim());
        entity.SwiftNumber = input.SwiftNumber?.Trim();
        entity.Website = input.Website?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new BankMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Banks.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
