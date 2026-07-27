using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class WarehouseAccountDto : EntityDto<Guid>
{
    public Guid WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public Guid? StockReceivedButNotBilledAccountId { get; set; }
    public Guid? StockDeliveredButNotBilledAccountId { get; set; }
    public Guid? StockAdjustmentAccountId { get; set; }
}

public class CreateWarehouseAccountDto
{
    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? StockReceivedButNotBilledAccountId { get; set; }
    public Guid? StockDeliveredButNotBilledAccountId { get; set; }
    public Guid? StockAdjustmentAccountId { get; set; }
}
