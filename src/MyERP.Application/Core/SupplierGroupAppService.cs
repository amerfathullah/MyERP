using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize(MyERPPermissions.SupplierGroups.Default)]
public class SupplierGroupAppService : MyERPAppService, ISupplierGroupAppService
{
    private readonly IRepository<SupplierGroup, Guid> _repository;

    public SupplierGroupAppService(IRepository<SupplierGroup, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SupplierGroupDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new SupplierGroupMapper().Map(entity);
        if (entity.ParentId.HasValue)
        {
            var parent = await _repository.FindAsync(entity.ParentId.Value);
            dto.ParentName = parent?.Name;
        }
        return dto;
    }

    public async Task<PagedResultDto<SupplierGroupDto>> GetListAsync(GetSupplierGroupListDto input)
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

        var dtos = entities.Select(e => new SupplierGroupMapper().Map(e)).ToList();
        return new PagedResultDto<SupplierGroupDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.SupplierGroups.Create)]
    public async Task<SupplierGroupDto> CreateAsync(CreateUpdateSupplierGroupDto input)
    {
        var newId = GuidGenerator.Create();
        var parentId = await ResolveDefaultRootParentAsync(input.ParentId, newId, input.Name);
        var entity = new SupplierGroup(newId, input.Name, parentId, input.IsGroup, CurrentTenant.Id)
        {
            DefaultPaymentTermsTemplateId = input.DefaultPaymentTermsTemplateId,
        };

        await _repository.InsertAsync(entity);
        return new SupplierGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.SupplierGroups.Edit)]
    public async Task<SupplierGroupDto> UpdateAsync(Guid id, CreateUpdateSupplierGroupDto input)
    {
        if (input.ParentId == id)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "A supplier group cannot be its own parent.");
        }

        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.ParentId = await ResolveDefaultRootParentAsync(input.ParentId, id, input.Name);
        entity.IsGroup = input.IsGroup;
        entity.DefaultPaymentTermsTemplateId = input.DefaultPaymentTermsTemplateId;

        await _repository.UpdateAsync(entity);
        return new SupplierGroupMapper().Map(entity);
    }

    private async Task<Guid?> ResolveDefaultRootParentAsync(Guid? parentId, Guid currentId, string currentName)
    {
        if (parentId.HasValue && parentId.Value != Guid.Empty)
        {
            return parentId.Value == currentId ? null : parentId;
        }

        var query = await _repository.GetQueryableAsync();
        var root = query
            .Where(x => x.ParentId == null && x.IsGroup)
            .OrderBy(x => x.CreationTime)
            .FirstOrDefault();

        if (root != null && root.Id != currentId && !string.Equals(root.Name, currentName, StringComparison.OrdinalIgnoreCase))
        {
            return root.Id;
        }

        return null;
    }

    [Authorize(MyERPPermissions.SupplierGroups.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
