using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemGroupDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
}

public class CreateItemGroupDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
}
