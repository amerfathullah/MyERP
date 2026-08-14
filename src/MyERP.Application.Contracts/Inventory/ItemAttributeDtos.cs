using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemAttributeDto : EntityDto<Guid>
{
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
