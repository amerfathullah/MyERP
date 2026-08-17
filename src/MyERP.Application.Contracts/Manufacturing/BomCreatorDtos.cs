using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Manufacturing;

public class BomCreatorItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid FgItemId { get; set; }
    public bool IsExpandable { get; set; }
    public decimal Qty { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Uom { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;
    public string StockUom { get; set; } = "Unit";
    public Guid? OperationId { get; set; }
    public bool IsSubcontracted { get; set; }
    public bool IsPhantomItem { get; set; }
    public bool SourcedBySupplier { get; set; }
    public string? Instruction { get; set; }
    public bool BomCreated { get; set; }
}

public class BomCreatorDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FinishedGoodItemId { get; set; }
    public decimal Qty { get; set; }
    public string? Uom { get; set; }
    public bool IsPhantom { get; set; }
    public Guid? RoutingId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public string RmCostAsPer { get; set; } = null!;
    public decimal RawMaterialCost { get; set; }
    public string? Remarks { get; set; }
    public int Status { get; set; }
    public string? ErrorLog { get; set; }
    public List<BomCreatorItemDto> Items { get; set; } = new();
}

public class CreateUpdateBomCreatorDto
{
    public Guid CompanyId { get; set; }
    public Guid FinishedGoodItemId { get; set; }
    public decimal Qty { get; set; } = 1;
    public string? Uom { get; set; }
    public bool IsPhantom { get; set; }
    public Guid? RoutingId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public string RmCostAsPer { get; set; } = "Valuation Rate";
    public string? Remarks { get; set; }
    public List<BomCreatorItemDto> Items { get; set; } = new();
}
