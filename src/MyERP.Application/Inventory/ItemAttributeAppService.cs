using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Manages Item Attributes for variant generation.
/// Attributes define configurable dimensions (Color, Size, Material, etc.)
/// that can be combined to create item variants from template items.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class ItemAttributeAppService : ApplicationService
{
    private readonly IRepository<ItemAttribute, Guid> _repository;

    public ItemAttributeAppService(IRepository<ItemAttribute, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ItemAttributeDto> GetAsync(Guid id)
    {
        var attr = await _repository.GetAsync(id);
        return MapToDto(attr);
    }

    public async Task<List<ItemAttributeDto>> GetListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var list = query.OrderBy(a => a.AttributeName).ToList();
        return list.Select(MapToDto).ToList();
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
        return MapToDto(attr);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ItemAttributeDto> AddValueAsync(Guid id, ItemAttributeValueDto input)
    {
        var attr = await _repository.GetAsync(id);
        attr.AddValue(input.Value, input.Abbreviation);
        await _repository.UpdateAsync(attr);
        return MapToDto(attr);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static ItemAttributeDto MapToDto(ItemAttribute a) => new()
    {
        Id = a.Id,
        Name = a.AttributeName,
        IsNumeric = a.IsNumeric,
        FromRange = a.FromRange ?? 0,
        ToRange = a.ToRange ?? 0,
        Increment = a.Increment ?? 0,
        Values = a.Values.Select(v => new ItemAttributeValueDto
        {
            Value = v.AttributeValue,
            Abbreviation = v.Abbreviation
        }).ToList()
    };
}

#region DTOs

public class ItemAttributeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsNumeric { get; set; }
    public decimal FromRange { get; set; }
    public decimal ToRange { get; set; }
    public decimal Increment { get; set; }
    public List<ItemAttributeValueDto> Values { get; set; } = new();
}

public class ItemAttributeValueDto
{
    public string Value { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
}

public class CreateItemAttributeDto
{
    public string Name { get; set; } = null!;
    public bool IsNumeric { get; set; }
    public decimal FromRange { get; set; }
    public decimal ToRange { get; set; }
    public decimal Increment { get; set; }
    public List<ItemAttributeValueDto> Values { get; set; } = new();
}

#endregion
