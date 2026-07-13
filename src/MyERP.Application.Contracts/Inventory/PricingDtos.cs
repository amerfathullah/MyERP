using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

// === Price List DTOs ===

public class PriceListDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public bool IsSelling { get; set; }
    public bool IsBuying { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public Guid? CompanyId { get; set; }
}

public class CreateUpdatePriceListDto
{
    [Required][StringLength(PriceListConsts.MaxNameLength)] public string Name { get; set; } = null!;
    [Required][StringLength(PriceListConsts.MaxCurrencyCodeLength)] public string CurrencyCode { get; set; } = "MYR";
    public bool IsSelling { get; set; }
    public bool IsBuying { get; set; }
    public bool IsDefault { get; set; }
    public Guid? CompanyId { get; set; }
}

// === Item Price DTOs ===

public class ItemPriceDto : AuditedEntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid PriceListId { get; set; }
    public string? PriceListName { get; set; }
    public decimal PriceListRate { get; set; }
    public string Uom { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal MinQty { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? BatchNo { get; set; }
}

public class CreateUpdateItemPriceDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public Guid PriceListId { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal PriceListRate { get; set; }
    [StringLength(ItemPriceConsts.MaxUomLength)] public string Uom { get; set; } = "Unit";
    [StringLength(ItemPriceConsts.MaxCurrencyCodeLength)] public string CurrencyCode { get; set; } = "MYR";
    public decimal MinQty { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    [StringLength(ItemPriceConsts.MaxBatchNoLength)] public string? BatchNo { get; set; }
}

public class GetItemPriceListDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? PriceListId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
}

/// <summary>Request to resolve the best price for a given item context.</summary>
public class GetItemRateRequestDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public Guid PriceListId { get; set; }
    public decimal Qty { get; set; } = 1;
    public DateTime? TransactionDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? BatchNo { get; set; }
}

public class ItemRateResultDto
{
    public decimal Rate { get; set; }
    public Guid? ItemPriceId { get; set; }
    public string? Source { get; set; }
}
