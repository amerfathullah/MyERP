using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.OpportunityLostReasons.Default)]
public class OpportunityLostReasonAppService : MyERPAppService, IOpportunityLostReasonAppService
{
    private readonly IRepository<OpportunityLostReason, Guid> _repository;

    public OpportunityLostReasonAppService(IRepository<OpportunityLostReason, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OpportunityLostReasonDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new OpportunityLostReasonMapper().Map(entity);
    }

    public async Task<PagedResultDto<OpportunityLostReasonDto>> GetListAsync(GetOpportunityLostReasonListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (input.IsDisabled.HasValue)
            query = query.Where(x => x.IsDisabled == input.IsDisabled.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.Reason.Contains(input.Filter));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Reason)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        return new PagedResultDto<OpportunityLostReasonDto>(
            totalCount,
            entities.Select(e => new OpportunityLostReasonMapper().Map(e)).ToList());
    }

    [Authorize(MyERPPermissions.OpportunityLostReasons.Create)]
    public async Task<OpportunityLostReasonDto> CreateAsync(CreateUpdateOpportunityLostReasonDto input)
    {
        var entity = new OpportunityLostReason(GuidGenerator.Create(), input.CompanyId, input.Reason, CurrentTenant.Id)
        {
            Description = input.Description,
            IsDisabled = input.IsDisabled,
        };

        await _repository.InsertAsync(entity);
        return new OpportunityLostReasonMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.OpportunityLostReasons.Edit)]
    public async Task<OpportunityLostReasonDto> UpdateAsync(Guid id, CreateUpdateOpportunityLostReasonDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Reason = input.Reason;
        entity.Description = input.Description;
        entity.IsDisabled = input.IsDisabled;

        await _repository.UpdateAsync(entity);
        return new OpportunityLostReasonMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.OpportunityLostReasons.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
