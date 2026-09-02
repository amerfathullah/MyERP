using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemBarcodeDto : EntityDto<Guid>
{
    public string Barcode { get; set; } = null!;
    public BarcodeType BarcodeType { get; set; }
    public bool IsDefault { get; set; }
}

public class ItemSupplierDto : EntityDto<Guid>
{
    public Guid SupplierId { get; set; }
    public string? SupplierPartNo { get; set; }
}

public class ItemCustomerDetailDto : EntityDto<Guid>
{
    public Guid CustomerId { get; set; }
    public string RefCode { get; set; } = null!;
}

public class ItemReorderDto : EntityDto<Guid>
{
    public Guid WarehouseId { get; set; }
    public Guid? WarehouseGroupId { get; set; }
    public decimal WarehouseReorderLevel { get; set; }
    public decimal WarehouseReorderQty { get; set; }
    public MyERP.Purchasing.MaterialRequestType MaterialRequestType { get; set; }
}

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
    public Guid? DefaultInventoryAccountId { get; set; }
    public bool GrantCommission { get; set; } = true;
    public decimal? MaxDiscount { get; set; }
    public bool IsActive { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQty { get; set; }
    public decimal SafetyStock { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public decimal MinOrderQty { get; set; }
    public bool InspectionRequiredBeforePurchase { get; set; }
    public bool InspectionRequiredBeforeDelivery { get; set; }
    public Guid? CustomsTariffNumberId { get; set; }
    public bool AllowAlternativeItem { get; set; }
    public Guid? DefaultManufacturerId { get; set; }
    public string? DefaultManufacturerPartNo { get; set; }
    public decimal TotalStockQty { get; set; }
    public bool IsLowStock { get; set; }
    public bool HasSerialNo { get; set; }
    public bool HasBatchNo { get; set; }
    public bool HasVariants { get; set; }
    public int LeadTimeDays { get; set; }
    public List<ItemBarcodeDto> Barcodes { get; set; } = new();
    public List<ItemSupplierDto> Suppliers { get; set; } = new();
    public List<ItemCustomerDetailDto> CustomerDetails { get; set; } = new();
    public List<ItemReorderDto> Reorders { get; set; } = new();
}
