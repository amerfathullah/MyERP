using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public class GetItemListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
}

/// <summary>
/// DTO for creating an item variant from a template item.
/// Per ERPNext: variant naming uses template code + attribute abbreviations.
/// </summary>
public class CreateItemVariantDto
{
    public List<VariantAttributeDto> Attributes { get; set; } = new();
}

public class VariantAttributeDto
{
    public Guid AttributeId { get; set; }
    public string Value { get; set; } = null!;
}

public interface IItemAppService :
    ICrudAppService<
        ItemDto,
        Guid,
        GetItemListDto,
        CreateUpdateItemDto>
{
    Task<ItemDto> CreateVariantAsync(Guid templateItemId, CreateItemVariantDto input);
}
