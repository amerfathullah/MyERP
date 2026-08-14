using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Manages Item Attributes for variant generation.
/// Attributes define configurable dimensions (Color, Size, Material, etc.)
/// that can be combined to create item variants from template items.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class ItemAttributeAppService : ApplicationService, IItemAttributeAppService
{
    private readonly IRepository<ItemAttribute, Guid> _repository;

    public ItemAttributeAppService(IRepository<ItemAttribute, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ItemAttributeDto> GetAsync(Guid id)
    {
        var attr = await _repository.GetAsync(id);
        return ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(attr);
    }

    public async Task<List<ItemAttributeDto>> GetListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var list = query.OrderBy(a => a.AttributeName).ToList();
        return list.Select(x => ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(x)).ToList();
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemAttributeDto> CreateAsync(CreateItemAttributeDto input)
    {
        var attr = new ItemAttribute(GuidGenerator.Create(), input.Name, input.IsNumeric, CurrentTenant.Id);

        if (input.IsNumeric)
        {
            attr.SetNumericRange(input.FromRange, input.ToRange, input.Increment);
        }
        else
        {
            foreach (var value in input.Values)
            {
                attr.AddValue(value.Value, value.Abbreviation);
            }
        }

        await _repository.InsertAsync(attr);
        return ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(attr);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ItemAttributeDto> AddValueAsync(Guid id, ItemAttributeValueDto input)
    {
        var attr = await _repository.GetAsync(id);
        attr.AddValue(input.Value, input.Abbreviation);
        await _repository.UpdateAsync(attr);
        return ObjectMapper.Map<ItemAttribute, ItemAttributeDto>(attr);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
