using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class CreateUpdateItemDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(ItemConsts.MaxCodeLength)]
    public string ItemCode { get; set; } = null!;

    [Required]
    [StringLength(ItemConsts.MaxNameLength)]
    public string ItemName { get; set; } = null!;

    [StringLength(ItemConsts.MaxBarcodeLength)]
    public string? Barcode { get; set; }

    [StringLength(ItemConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    [Required]
    public ItemType ItemType { get; set; }

    [StringLength(ItemConsts.MaxGroupLength)]
    public string? ItemGroup { get; set; }

    [StringLength(ItemConsts.MaxBrandLength)]
    public string? Brand { get; set; }

    [Required]
    [StringLength(ItemConsts.MaxUomLength)]
    public string Uom { get; set; } = "Unit";

    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.FIFO;

    [Range(0, double.MaxValue)]
    public decimal? StandardSellingPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? StandardBuyingPrice { get; set; }

    public Guid? TaxCategoryId { get; set; }

    public bool MaintainStock { get; set; } = true;

    public Guid? DefaultIncomeAccountId { get; set; }

    public Guid? DefaultExpenseAccountId { get; set; }

    public bool IsActive { get; set; } = true;
}
