using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class CreateUpdateWarehouseDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public Guid? BranchId { get; set; }

    [Required]
    [StringLength(WarehouseConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(WarehouseConsts.MaxCodeLength)]
    public string? WarehouseCode { get; set; }

    [StringLength(WarehouseConsts.MaxAddressLength)]
    public string? Address { get; set; }

    [StringLength(WarehouseConsts.MaxCityLength)]
    public string? City { get; set; }

    [StringLength(WarehouseConsts.MaxStateLength)]
    public string? State { get; set; }

    [StringLength(WarehouseConsts.MaxPostalCodeLength)]
    public string? PostalCode { get; set; }

    [StringLength(WarehouseConsts.MaxCountryLength)]
    public string? Country { get; set; }

    public Guid? ParentWarehouseId { get; set; }

    public bool IsGroup { get; set; }

    public bool IsActive { get; set; } = true;
}
