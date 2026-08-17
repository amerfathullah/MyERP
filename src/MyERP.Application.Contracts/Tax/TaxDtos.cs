using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Tax;

public class TaxCategoryDto : FullAuditedEntityDto<Guid>
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string TaxType { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class CreateUpdateTaxCategoryDto
{
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public TaxType TaxType { get; set; }

    public bool IsActive { get; set; } = true;
}

public class TaxRuleDto : EntityDto<Guid>
{
    public Guid TaxCategoryId { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ItemGroupFilter { get; set; }
    public string? RegionFilter { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateTaxRuleDto
{
    [Required]
    public Guid TaxCategoryId { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal Rate { get; set; }

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [StringLength(100)]
    public string? ItemGroupFilter { get; set; }

    [StringLength(100)]
    public string? RegionFilter { get; set; }

    public int Priority { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Default tax line for form auto-population.
/// Returned by GetDefaultTaxLinesAsync for selling/buying transactions.
/// Frontend uses this to pre-populate tax cascade calculation.
/// </summary>
public class DefaultTaxLineDto
{
    public string TaxName { get; set; } = null!;
    public decimal Rate { get; set; }
    public string ChargeType { get; set; } = "OnNetTotal";
    public Guid? AccountId { get; set; }
    public string? TaxCategoryCode { get; set; }
}

public class ItemTaxTemplateDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public ItemTaxTemplateDetailDto[] Details { get; set; } = [];
}

public class ItemTaxTemplateDetailDto
{
    public Guid Id { get; set; }
    public Guid TaxAccountId { get; set; }
    public decimal TaxRate { get; set; }
    public bool NotApplicable { get; set; }
}

public class CreateItemTaxTemplateDto
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public CreateItemTaxTemplateDetailDto[] Details { get; set; } = [];
}

public class UpdateItemTaxTemplateDto
{
    public string Title { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public CreateItemTaxTemplateDetailDto[] Details { get; set; } = [];
}

public class CreateItemTaxTemplateDetailDto
{
    public Guid TaxAccountId { get; set; }
    public decimal TaxRate { get; set; }
    public bool NotApplicable { get; set; }
}

public class GetTaxTemplateListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public TaxTemplateType? TemplateType { get; set; }
    public string? Filter { get; set; }
}

public class TaxChargesTemplateDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public TaxTemplateType TemplateType { get; set; }
    public Guid? TaxCategoryId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; }
    public List<TaxChargesTemplateRowDto> Rows { get; set; } = new();
}

public class TaxChargesTemplateRowDto
{
    public Guid Id { get; set; }
    public int RowIndex { get; set; }
    public string ChargeType { get; set; } = "On Net Total";
    public decimal Rate { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public string TaxCategory { get; set; } = "Total";
    public int? ReferenceRowIndex { get; set; }
    public bool IncludedInPrintRate { get; set; }
    public string? Description { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class CreateTaxChargesTemplateDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = "";
    public TaxTemplateType TemplateType { get; set; }
    public Guid? TaxCategoryId { get; set; }
    public bool IsDefault { get; set; }
    public List<CreateTaxChargesTemplateRowDto> Rows { get; set; } = new();
}

public class CreateTaxChargesTemplateRowDto
{
    public string ChargeType { get; set; } = "On Net Total";
    public decimal Rate { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? TaxCategory { get; set; }
    public int? ReferenceRowIndex { get; set; }
    public bool IncludedInPrintRate { get; set; }
    public string? Description { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class TaxSummaryDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Output Tax (Sales)
    public decimal TotalSalesAmount { get; set; }
    public decimal OutputTax { get; set; }
    public decimal CreditNoteTaxAdjustment { get; set; }
    public decimal NetOutputTax { get; set; }
    public int SalesInvoiceCount { get; set; }
    public int CreditNoteCount { get; set; }

    // Input Tax (Purchases)
    public decimal TotalPurchaseAmount { get; set; }
    public decimal InputTax { get; set; }
    public decimal DebitNoteTaxAdjustment { get; set; }
    public decimal NetInputTax { get; set; }
    public int PurchaseInvoiceCount { get; set; }
    public int DebitNoteCount { get; set; }

    // Net Position
    public decimal NetTaxPayable { get; set; }
    public bool IsRefundable { get; set; }

    // Rate Breakdowns
    public List<TaxRateBreakdownDto> OutputTaxBreakdown { get; set; } = new();
    public List<TaxRateBreakdownDto> InputTaxBreakdown { get; set; } = new();
}

public class TaxRateBreakdownDto
{
    public string TaxRate { get; set; } = null!;
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public int InvoiceCount { get; set; }
}

public class Sst02FilingDataDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? SstRegistrationNumber { get; set; }
    public string TaxPeriod { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Section A: Taxable Supplies by Rate
    public decimal TaxableSupplies6Percent { get; set; }
    public decimal TaxableSupplies10Percent { get; set; }
    public decimal TaxableSupplies5Percent { get; set; }
    public decimal TaxableSuppliesOtherRate { get; set; }

    // Section B: Exempt Supplies
    public decimal ExemptSupplies { get; set; }

    // Section C: Zero-Rated Supplies
    public decimal ZeroRatedSupplies { get; set; }

    // Section D: Output Tax
    public decimal OutputTax6Percent { get; set; }
    public decimal OutputTax10Percent { get; set; }
    public decimal OutputTax5Percent { get; set; }
    public decimal OutputTaxOther { get; set; }
    public decimal TotalOutputTax { get; set; }

    // Section E: Input Tax Credit
    public decimal InputTaxCredit { get; set; }

    // Section F: Adjustments
    public decimal CreditNoteAdjustment { get; set; }
    public decimal DebitNoteAdjustment { get; set; }
    public decimal BadDebtRelief { get; set; }
    public decimal NetAdjustment { get; set; }

    // Section G: Net Tax Position
    public decimal NetTaxPayable { get; set; }
    public bool IsRefundable { get; set; }

    // Supporting counts
    public int TotalSalesInvoices { get; set; }
    public int TotalPurchaseInvoices { get; set; }
    public int TotalCreditNotes { get; set; }
    public int TotalDebitNotes { get; set; }
}
