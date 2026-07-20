using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PaymentScheduleDto
{
    public Guid Id { get; set; }
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }
}

public class SalesInvoiceDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
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
    public string? EInvoiceStatus { get; set; }
    public string? LhdnUuid { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }
    public Guid DebitToAccountId { get; set; }
    public List<SalesInvoiceItemDto> Items { get; set; } = new();
}

public class SalesInvoiceItemDto
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

public class CreateSalesInvoiceDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public DateTime IssueDate { get; set; }

    public DateTime? DueDate { get; set; }

    [StringLength(SalesInvoiceConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    public string? Notes { get; set; }

    public Guid? PaymentTermsTemplateId { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>Mark as opening balance invoice (data migration). Blocks update_stock, clears payment terms.</summary>
    public bool IsOpening { get; set; }

    /// <summary>Link to project for timesheet-based billing (auto-fetches unbilled timesheets).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>When true, stock is deducted on invoice submit (POS/direct sale without DN).</summary>
    public bool UpdateStock { get; set; }

    /// <summary>Warehouse for stock deduction when UpdateStock=true.</summary>
    public Guid? WarehouseId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateSalesInvoiceItemDto> Items { get; set; } = new();
}

public class CreateSalesInvoiceItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    /// <summary>Quantity (positive for normal invoices, negative for credit notes/returns).</summary>
    [Required]
    public decimal Quantity { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; }

    [StringLength(20)]
    public string Uom { get; set; } = "Unit";
}
