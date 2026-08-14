using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Sales/Purchase Taxes and Charges Template — reusable tax configuration applied to transactions.
/// Maps to ERPNext accounts/doctype/sales_taxes_and_charges_template +
///   accounts/doctype/purchase_taxes_and_charges_template.
///
/// Per ERPNext:
/// - Template is company-scoped (one template per company)
/// - Can be marked as default (auto-applied to new transactions)
/// - Only one default per (company, tax_category) combination
/// - Disabled templates cannot be used on new transactions
/// - Contains ordered list of tax/charge rows with cumulative calculation
///
/// Per DO-NOT: "Allow duplicate active Sales/Purchase Tax Templates for same (company, tax_category)"
/// Per DO-NOT: "Allow transactions with a disabled Sales/Purchase Taxes and Charges template"
/// </summary>
public class TaxChargesTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Template name (e.g., "Malaysia SST 6%", "Zero-Rated Export").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Whether this is a Selling or Buying template.</summary>
    public TaxTemplateType TemplateType { get; set; }

    /// <summary>Tax category for auto-selection (matches customer/supplier tax category).</summary>
    public Guid? TaxCategoryId { get; set; }

    /// <summary>Default template for new transactions in this company + category.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Whether this template is enabled for use.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Tax rows (ordered by RowIndex for sequential calculation).</summary>
    private readonly List<TaxChargesTemplateRow> _rows = new();
    public IReadOnlyList<TaxChargesTemplateRow> Rows => _rows.AsReadOnly();

    protected TaxChargesTemplate() { }

    public TaxChargesTemplate(Guid id, Guid companyId, string name, TaxTemplateType templateType, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TemplateType = templateType;
        TenantId = tenantId;
    }

    public void AddRow(TaxChargesTemplateRow row)
    {
        row.RowIndex = _rows.Count;
        _rows.Add(row);
    }

    public void ClearRows()
    {
        _rows.Clear();
    }

    /// <summary>
    /// Validates template integrity before save.
    /// Per ERPNext: row_id references must be valid, Actual rows can't reference others,
    /// no forward references, no first-row references.
    /// </summary>
    public void Validate()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];

            // Actual charge type cannot reference another row
            if (row.ChargeType == "Actual" && row.ReferenceRowIndex.HasValue)
                throw new BusinessException("MyERP:Tax:ActualCannotReference");

            // Reference row must be a previous row (no forward references)
            if (row.ReferenceRowIndex.HasValue)
            {
                if (row.ReferenceRowIndex.Value >= i)
                    throw new BusinessException("MyERP:Tax:ForwardReference");
                if (row.ReferenceRowIndex.Value < 0)
                    throw new BusinessException("MyERP:Tax:InvalidRowRef");
            }

            // "On Previous Row" types require a reference
            if (row.ChargeType is "On Previous Row Amount" or "On Previous Row Total"
                && !row.ReferenceRowIndex.HasValue)
            {
                throw new BusinessException("MyERP:Tax:MissingRowRef")
                    .WithData("chargeType", row.ChargeType);
            }
        }
    }
}

/// <summary>
/// Single tax/charge row within a template.
/// Defines how one tax is calculated (rate, charge type, account assignment).
/// </summary>
public class TaxChargesTemplateRow : Entity<Guid>
{
    public Guid TaxChargesTemplateId { get; set; }

    /// <summary>Row ordering for sequential calculation.</summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Charge type determines calculation method:
    /// - "On Net Total" — rate% of document net total
    /// - "On Previous Row Amount" — rate% of specified previous row's tax amount
    /// - "On Previous Row Total" — rate% of cumulative total up to specified row
    /// - "On Item Quantity" — rate × quantity (per-unit charge, e.g., excise duty)
    /// - "Actual" — fixed amount distributed proportionally across items
    /// </summary>
    public string ChargeType { get; set; } = "On Net Total";

    /// <summary>Tax rate (percentage for most charge types, amount for Actual).</summary>
    public decimal Rate { get; set; }

    /// <summary>GL Account to post this tax to.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Account description/name (denormalized for display).</summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Tax category for GL: "Total" (adds to grand total), "Valuation" (adds to item cost),
    /// "Valuation and Total" (both).
    /// Per ERPNext: "Total" default for selling, "Valuation and Total" for buying with stock.
    /// </summary>
    public string TaxCategory { get; set; } = "Total";

    /// <summary>Reference to another row index (for "On Previous Row" charge types).</summary>
    public int? ReferenceRowIndex { get; set; }

    /// <summary>Whether this tax is included in the item print rate (inclusive pricing).</summary>
    public bool IncludedInPrintRate { get; set; }

    /// <summary>Whether this tax is included in the paid amount (for Payment Entry taxes).</summary>
    public bool IncludedInPaidAmount { get; set; }

    /// <summary>Cost center for this tax (optional, for departmental GL reporting).</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Description/label shown on invoice (e.g., "SST @ 6%", "Service Tax").</summary>
    public string? Description { get; set; }

    protected TaxChargesTemplateRow() { }

    public TaxChargesTemplateRow(Guid id, Guid templateId, string chargeType, decimal rate,
        Guid? accountId = null, string? description = null)
        : base(id)
    {
        TaxChargesTemplateId = templateId;
        ChargeType = chargeType;
        Rate = rate;
        AccountId = accountId;
        Description = description;
    }
}
