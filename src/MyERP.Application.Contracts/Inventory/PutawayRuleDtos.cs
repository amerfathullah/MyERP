using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class PutawayRuleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal StockCapacity { get; set; }
    public int Priority { get; set; }
    public string? Uom { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateUpdatePutawayRuleDto
{
    public Guid CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal StockCapacity { get; set; }
    public int Priority { get; set; } = 1;
    public string? Uom { get; set; }
}
