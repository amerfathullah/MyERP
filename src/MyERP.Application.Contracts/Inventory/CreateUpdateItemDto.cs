using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class CreateItemBarcodeDto
{
    [Required]
    [StringLength(100)]
    public string Barcode { get; set; } = null!;

    public BarcodeType BarcodeType { get; set; } = BarcodeType.Ean;
    public bool IsDefault { get; set; }
}

public class CreateItemSupplierDto
{
    [Required]
    public Guid SupplierId { get; set; }

    [StringLength(100)]
    public string? SupplierPartNo { get; set; }
}

public class CreateItemCustomerDetailDto
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [StringLength(100)]
    public string RefCode { get; set; } = null!;
}

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

    // Reorder settings
    [Range(0, double.MaxValue)]
    public decimal ReorderLevel { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ReorderQty { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SafetyStock { get; set; }

    public Guid? DefaultWarehouseId { get; set; }

    /// <summary>Minimum order quantity for purchasing.</summary>
    [Range(0, double.MaxValue)]
    public decimal MinOrderQty { get; set; }

    /// <summary>Require QI before Purchase Receipt submission.</summary>
    public bool InspectionRequiredBeforePurchase { get; set; }

    /// <summary>Require QI before Delivery Note submission.</summary>
    public bool InspectionRequiredBeforeDelivery { get; set; }

    /// <summary>Customs Tariff Number (HS Code) for export/import declaration.</summary>
    public Guid? CustomsTariffNumberId { get; set; }

    /// <summary>Allow alternative/substitute item in transactions.</summary>
    public bool AllowAlternativeItem { get; set; }

    /// <summary>Default item manufacturer.</summary>
    public Guid? DefaultManufacturerId { get; set; }

    /// <summary>Default manufacturer part number.</summary>
    [StringLength(100)]
    public string? DefaultManufacturerPartNo { get; set; }

    /// <summary>Additional barcodes for this item (case codes, alternate symbologies, etc).</summary>
    public List<CreateItemBarcodeDto> Barcodes { get; set; } = new();

    /// <summary>Preferred/approved suppliers for this item.</summary>
    public List<CreateItemSupplierDto> Suppliers { get; set; } = new();

    /// <summary>Customer-specific item codes for this item.</summary>
    public List<CreateItemCustomerDetailDto> CustomerDetails { get; set; } = new();
}
