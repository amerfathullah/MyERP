using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class StockClosingEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime ToDate { get; set; }
    public int Status { get; set; }
    public int TotalEntries { get; set; }
    public decimal TotalStockValue { get; set; }
    public Guid? PreviousClosingEntryId { get; set; }
    public DateTime? ScannedFromDate { get; set; }
    public DateTime CreationTime { get; set; }
    public List<StockClosingBalanceDto>? Balances { get; set; }
}

public class StockClosingBalanceDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal Qty { get; set; }
    public decimal StockValue { get; set; }
    public decimal ValuationRate { get; set; }
}

public class CreateStockClosingDto
{
    public Guid CompanyId { get; set; }
    public DateTime ToDate { get; set; }
}
