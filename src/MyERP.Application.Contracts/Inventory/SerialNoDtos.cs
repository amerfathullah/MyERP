using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class SerialNoDto : EntityDto<Guid>
{
    public string SerialNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal PurchaseRate { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? AmcExpiryDate { get; set; }
    public string MaintenanceStatus { get; set; } = null!;
    public int Status { get; set; }
    public DateTime CreationTime { get; set; }
}

public class GetSerialNoListDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Filter { get; set; }
}
