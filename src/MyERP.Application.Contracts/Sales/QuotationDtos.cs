using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class QuotationDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string QuotationNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = null!;
    public Guid? ConvertedToSalesOrderId { get; set; }
    public List<QuotationItemDto> Items { get; set; } = new();
}

public class QuotationItemDto
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

public class CreateQuotationDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public DateTime IssueDate { get; set; }

    public DateTime? ValidUntil { get; set; }

    [StringLength(QuotationConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    [StringLength(QuotationConsts.MaxTermsLength)]
    public string? Terms { get; set; }

    [StringLength(QuotationConsts.MaxNoteLength)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateQuotationItemDto> Items { get; set; } = new();
}

public class CreateQuotationItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; }

    [StringLength(20)]
    public string Uom { get; set; } = "Unit";
}
