using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemWhereUsedRequestDto
{
    public Guid ItemId { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Section { get; set; } // null = All, "Where Used", "References"
}

public class ItemWhereUsedRowDto
{
    public string Section { get; set; } = null!; // "Where Used" or "References"
    public string ReferenceType { get; set; } = null!; // "BOM Component", "BOM Secondary Item", "Product Bundle Component", etc.
    public string DocumentType { get; set; } = null!; // "BOM", "Product Bundle", "Subcontracting BOM", "Item", "Item Alternative"
    public string DocumentName { get; set; } = null!; // e.g. BOM-001
    public Guid? DocumentId { get; set; }
    public Guid? RelatedItemId { get; set; }
    public string? RelatedItemCode { get; set; }
    public string? RelatedItemName { get; set; }
    public decimal? Quantity { get; set; }
    public string? Uom { get; set; }
    public decimal? StockQuantity { get; set; }
    public string? StockUom { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsActive { get; set; }
    public string? Details { get; set; }
}

public class ItemWhereUsedReportDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public List<ItemWhereUsedRowDto> Rows { get; set; } = new();
    public int TotalWhereUsedCount { get; set; }
    public int TotalReferencesCount { get; set; }
}
