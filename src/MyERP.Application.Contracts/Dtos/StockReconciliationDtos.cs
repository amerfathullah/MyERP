using System;
using MyERP.Core;
using Volo.Abp.Application.Dtos;

namespace MyERP.Dtos;

public class StockReconciliationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? ReconciliationNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal DifferenceAmount { get; set; }
    public StockReconciliationItemDto[] Items { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class StockReconciliationItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal CurrentValuationRate { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal NewValuationRate { get; set; }
    public decimal QuantityDifference { get; set; }
    public decimal DifferenceAmount { get; set; }
}

public class CreateStockReconciliationDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public CreateStockReconciliationItemDto[] Items { get; set; } = [];
}

public class CreateStockReconciliationItemDto
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal NewValuationRate { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal CurrentValuationRate { get; set; }
}

public class GetStockReconciliationListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}
