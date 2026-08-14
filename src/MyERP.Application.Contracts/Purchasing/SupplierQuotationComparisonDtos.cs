using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class SupplierQuotationComparisonDto
{
    public Guid? RfqId { get; set; }
    public List<ComparisonSupplierDto> Suppliers { get; set; } = new();
    public List<ComparisonItemDto> Items { get; set; } = new();
    public decimal LowestTotalAmount { get; set; }
}

public class ComparisonSupplierDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public Guid QuotationId { get; set; }
    public string? QuotationNumber { get; set; }
    public string? Currency { get; set; }
    public DateTime? ValidTill { get; set; }
    public decimal GrandTotal { get; set; }
}

public class ComparisonItemDto
{
    public Guid ItemId { get; set; }
    public string ItemDescription { get; set; } = "";
    public List<ComparisonPriceDto> SupplierPrices { get; set; } = new();
    public decimal LowestRate { get; set; }
}

public class ComparisonPriceDto
{
    public Guid SupplierId { get; set; }
    public Guid QuotationId { get; set; }
    public decimal Rate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsQuoted { get; set; }
    public bool IsLowestPrice { get; set; }
}
