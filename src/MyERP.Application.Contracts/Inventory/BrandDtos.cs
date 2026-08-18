using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class BrandDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public Guid? DefaultIncomeAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateBrandDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public Guid? DefaultIncomeAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}
