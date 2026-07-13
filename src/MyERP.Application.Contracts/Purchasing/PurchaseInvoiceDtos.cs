using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class PurchaseInvoiceDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierTin { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
    public decimal BaseOutstandingAmount { get; set; }
    public string Status { get; set; } = null!;
    public string EInvoiceStatus { get; set; } = null!;
    public string? LhdnUuid { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }
    public List<PurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class PurchaseInvoiceItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreatePurchaseInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? PaymentTermsTemplateId { get; set; }
    [StringLength(100)] public string? SupplierInvoiceNumber { get; set; }
    [StringLength(3)] public string CurrencyCode { get; set; } = "MYR";
    public string? Notes { get; set; }
    [Required][MinLength(1)] public List<CreatePurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class CreatePurchaseInvoiceItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(500)] public string Description { get; set; } = null!;
    [Required][Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
    [StringLength(50)] public string Uom { get; set; } = "Unit";
}
