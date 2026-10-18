using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class BomSearchFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? Item1Id { get; set; }
    public Guid? Item2Id { get; set; }
    public Guid? Item3Id { get; set; }
    public Guid? Item4Id { get; set; }
    public Guid? Item5Id { get; set; }
    public List<Guid>? ItemIds { get; set; }
    public bool SearchSubAssemblies { get; set; }
    public string? SearchTerm { get; set; }
}

public class BomSearchRowDto
{
    public Guid ParentId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty; // "BOM" or "Product Bundle"
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public decimal? TotalCost { get; set; }
    public bool FoundInSubAssembly { get; set; }
    public string ComponentItemsSummary { get; set; } = string.Empty;
}

public class BomSearchReportDto
{
    public int TotalMatches { get; set; }
    public int BomMatchesCount { get; set; }
    public int ProductBundleMatchesCount { get; set; }
    public List<BomSearchRowDto> Rows { get; set; } = new();
}
