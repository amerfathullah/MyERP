using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class WarehouseDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = null!;
    public string? WarehouseCode { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? ParentWarehouseId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsActive { get; set; }
}
