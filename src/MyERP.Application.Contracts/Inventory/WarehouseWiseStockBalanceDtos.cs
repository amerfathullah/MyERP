using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class WarehouseWiseStockBalanceFilterDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public Guid? WarehouseId { get; set; }

    public bool ShowDisabledWarehouses { get; set; }

    public DateTime? AsOfDate { get; set; }
}

public class WarehouseStockBalanceItemDto
{
    public Guid WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string? WarehouseCode { get; set; }

    public Guid? ParentWarehouseId { get; set; }

    public string? ParentWarehouseName { get; set; }

    public bool IsGroup { get; set; }

    public bool IsActive { get; set; }

    public int Indent { get; set; }

    public decimal StockBalance { get; set; }

    public decimal DirectStockBalance { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal DirectQuantity { get; set; }

    public int ItemCount { get; set; }
}

public class WarehouseWiseStockBalanceReportDto
{
    public Guid CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public DateTime? AsOfDate { get; set; }

    public decimal TotalStockBalance { get; set; }

    public decimal TotalQuantity { get; set; }

    public int TotalWarehouseCount { get; set; }

    public int ActiveWarehouseCount { get; set; }

    public List<WarehouseStockBalanceItemDto> Items { get; set; } = new();
}
