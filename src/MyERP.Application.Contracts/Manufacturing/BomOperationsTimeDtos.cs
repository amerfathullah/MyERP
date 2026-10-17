using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class BomOperationsTimeRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BomId { get; set; }
    public Guid? WorkstationId { get; set; }
}

public class BomOperationsTimeReportDto
{
    public List<BomOperationsTimeRowDto> Rows { get; set; } = new();
    public int TotalBoms { get; set; }
    public int TotalOperations { get; set; }
    public decimal TotalTimeInMinutes { get; set; }
}

public class BomOperationsTimeRowDto
{
    public Guid BomId { get; set; }
    public string BomNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Uom { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public Guid? WorkstationId { get; set; }
    public string? WorkstationName { get; set; }
    public decimal TimeInMins { get; set; }
    public int UsedAsSubassemblyItems { get; set; }
}
