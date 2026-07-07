using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public ItemType ItemType { get; set; }
    public string? ItemGroup { get; set; }
    public string? Brand { get; set; }
    public string Uom { get; set; } = null!;
    public ValuationMethod ValuationMethod { get; set; }
    public decimal? StandardSellingPrice { get; set; }
    public decimal? StandardBuyingPrice { get; set; }
    public Guid? TaxCategoryId { get; set; }
    public bool MaintainStock { get; set; }
    public Guid? DefaultIncomeAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public bool IsActive { get; set; }
}
