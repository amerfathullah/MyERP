using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize(MyERPPermissions.CustomerGroups.Default)]
public class CustomerGroupAppService : MyERPAppService, ICustomerGroupAppService
{
    private readonly IRepository<CustomerGroup, Guid> _repository;

    public CustomerGroupAppService(IRepository<CustomerGroup, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CustomerGroupDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new CustomerGroupMapper().Map(entity);
        if (entity.ParentId.HasValue)
        {
            var parent = await _repository.FindAsync(entity.ParentId.Value);
            dto.ParentName = parent?.Name;
        }
        return dto;
    }

    public async Task<PagedResultDto<CustomerGroupDto>> GetListAsync(GetCustomerGroupListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ParentId.HasValue)
            query = query.Where(x => x.ParentId == input.ParentId.Value);
        if (input.IsGroup.HasValue)
            query = query.Where(x => x.IsGroup == input.IsGroup.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.Name.Contains(input.Filter));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new CustomerGroupMapper().Map(e)).ToList();
        return new PagedResultDto<CustomerGroupDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.CustomerGroups.Create)]
    public async Task<CustomerGroupDto> CreateAsync(CreateUpdateCustomerGroupDto input)
    {
        var entity = new CustomerGroup(GuidGenerator.Create(), input.Name, input.ParentId, input.IsGroup, CurrentTenant.Id)
        {
            DefaultPaymentTermsTemplateId = input.DefaultPaymentTermsTemplateId,
            DefaultPriceListId = input.DefaultPriceListId,
            DefaultCreditLimit = input.DefaultCreditLimit,
        };

        await _repository.InsertAsync(entity);
        return new CustomerGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CustomerGroups.Edit)]
    public async Task<CustomerGroupDto> UpdateAsync(Guid id, CreateUpdateCustomerGroupDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.ParentId = input.ParentId;
        entity.IsGroup = input.IsGroup;
        entity.DefaultPaymentTermsTemplateId = input.DefaultPaymentTermsTemplateId;
        entity.DefaultPriceListId = input.DefaultPriceListId;
        entity.DefaultCreditLimit = input.DefaultCreditLimit;

        await _repository.UpdateAsync(entity);
        return new CustomerGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CustomerGroups.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
