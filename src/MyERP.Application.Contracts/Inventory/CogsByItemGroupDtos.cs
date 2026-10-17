using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class CogsByItemGroupFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CogsByItemGroupRowDto
{
    public Guid? ItemGroupId { get; set; }
    public string ItemGroupName { get; set; } = string.Empty;
    public decimal CogsDebit { get; set; }
    public int Indent { get; set; }
    public bool IsGroup { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class CogsByItemGroupReportDto
{
    public List<CogsByItemGroupRowDto> Rows { get; set; } = new();
    public decimal TotalCogsDebit { get; set; }
    public int TotalItemGroups { get; set; }
    public string? TopItemGroupName { get; set; }
    public decimal TopItemGroupDebit { get; set; }
}
