using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.QuotationLostReasons.Default)]
public class QuotationLostReasonAppService : MyERPAppService, IQuotationLostReasonAppService
{
    private readonly IRepository<QuotationLostReason, Guid> _repository;

    public QuotationLostReasonAppService(IRepository<QuotationLostReason, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<QuotationLostReasonDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new QuotationLostReasonMapper().Map(entity);
    }

    public async Task<PagedResultDto<QuotationLostReasonDto>> GetListAsync(GetQuotationLostReasonListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Reason.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Reason)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new QuotationLostReasonMapper().Map(e)).ToList();
        return new PagedResultDto<QuotationLostReasonDto>(totalCount, dtos);
    }

    public async Task<List<QuotationLostReasonDto>> GetAllListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(
            query.Where(x => x.IsActive)
                 .OrderBy(x => x.Reason));

        return entities.Select(e => new QuotationLostReasonMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.QuotationLostReasons.Create)]
    public async Task<QuotationLostReasonDto> CreateAsync(CreateUpdateQuotationLostReasonDto input)
    {
        var entity = new QuotationLostReason(
            GuidGenerator.Create(),
            input.Reason,
            input.Description,
            CurrentTenant.Id)
        {
            IsActive = input.IsActive
        };

        await _repository.InsertAsync(entity);
        return new QuotationLostReasonMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.QuotationLostReasons.Edit)]
    public async Task<QuotationLostReasonDto> UpdateAsync(Guid id, CreateUpdateQuotationLostReasonDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Reason = input.Reason;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new QuotationLostReasonMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.QuotationLostReasons.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
