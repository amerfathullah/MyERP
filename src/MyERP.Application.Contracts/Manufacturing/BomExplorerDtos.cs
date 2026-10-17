using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class BomExplorerRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid BomId { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public Guid? WarehouseId { get; set; }
}

public class BomLookupDto
{
    public Guid Id { get; set; }
    public string BomNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public bool IsDefault { get; set; }
}

public class BomExplorerDto
{
    public Guid BomId { get; set; }
    public string BomNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal BaseQuantity { get; set; }
    public decimal ExplodedQuantity { get; set; }
    public string? Uom { get; set; }
    public List<BomExplorerRowDto> Rows { get; set; } = new();
    public int TotalComponents { get; set; }
    public int TotalSubBoms { get; set; }
    public decimal TotalCost { get; set; }
}

public class BomExplorerRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public int BomLevel { get; set; }
    public int Indent { get; set; }
    public Guid? SubBomId { get; set; }
    public string? SubBomNumber { get; set; }
    public decimal Qty { get; set; }
    public string? Uom { get; set; }
    public decimal StockQty { get; set; }
    public string? StockUom { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPhantom { get; set; }
    public bool DoNotExplode { get; set; }
    public string? Description { get; set; }
    public decimal AvailableQty { get; set; }
    public bool HasSufficientStock { get; set; }
}
