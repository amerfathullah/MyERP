using System;
using System.Collections.Generic;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class PickListDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? PickListNumber { get; set; }
    public string Purpose { get; set; } = null!;
    public Guid? SalesOrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public int Status { get; set; }
    public bool IsFullyTransferred { get; set; }
    public bool IsPartiallyTransferred { get; set; }
    public PickListItemDto[] Items { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class PickListItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal PendingQty { get; set; }
}

public class CreatePickListDto
{
    public Guid CompanyId { get; set; }
    public string Purpose { get; set; } = "Delivery";
    public Guid? SalesOrderId { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public CreatePickListItemDto[] Items { get; set; } = [];
}

public class CreatePickListItemDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public Guid? BatchId { get; set; }
}

public class PickAllocationResultDto
{
    public bool HasShortage { get; set; }
    public List<PickAllocationDto> Allocations { get; set; } = new();
}

public class PickAllocationDto
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal ShortageQty { get; set; }
}

public class PendingTransferDto
{
    public Guid PickListItemId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal PendingQty { get; set; }
    public Guid? BatchId { get; set; }
}
