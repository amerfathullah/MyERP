using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Item Tax Template — defines per-item tax rate overrides.
/// When assigned to an item, overrides the document-level tax rate for matching accounts.
/// Rate of "N/A" (not_applicable=true) means tax excluded entirely for that item.
/// Maps to ERPNext accounts/doctype/item_tax_template.
/// </summary>
public class ItemTaxTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Title { get; set; } = null!;
    public bool IsDisabled { get; set; }

    private readonly List<ItemTaxTemplateDetail> _details = new();
    public IReadOnlyList<ItemTaxTemplateDetail> Details => _details.AsReadOnly();

    protected ItemTaxTemplate() { }

    public ItemTaxTemplate(Guid id, Guid companyId, string title, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 200);
        TenantId = tenantId;
    }

    public void AddDetail(Guid taxAccountId, decimal taxRate, bool notApplicable = false)
    {
        if (_details.Any(d => d.TaxAccountId == taxAccountId))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Duplicate tax account in template");

        var effectiveRate = notApplicable ? 0 : taxRate;
        _details.Add(new ItemTaxTemplateDetail(Guid.NewGuid(), Id, taxAccountId, effectiveRate, notApplicable));
    }

    /// <summary>
    /// Get the tax rate for a specific account. Returns null if account not in template
    /// (meaning use document-level rate).
    /// </summary>
    public decimal? GetRateForAccount(Guid taxAccountId)
    {
        var detail = _details.FirstOrDefault(d => d.TaxAccountId == taxAccountId);
        if (detail == null) return null;
        if (detail.NotApplicable) return null; // N/A sentinel — exclude this tax entirely
        return detail.TaxRate;
    }
}

public class ItemTaxTemplateDetail : FullAuditedEntity<Guid>
{
    public Guid ItemTaxTemplateId { get; set; }
    public Guid TaxAccountId { get; set; }
    public decimal TaxRate { get; set; }

    /// <summary>If true, this tax is not applicable for items using this template.</summary>
    public bool NotApplicable { get; set; }

    protected ItemTaxTemplateDetail() { }

    public ItemTaxTemplateDetail(Guid id, Guid templateId, Guid taxAccountId,
        decimal taxRate, bool notApplicable) : base(id)
    {
        ItemTaxTemplateId = templateId;
        TaxAccountId = taxAccountId;
        TaxRate = taxRate;
        NotApplicable = notApplicable;
    }
}
