using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Tax category defines a type of tax (e.g., SST-Sales 6%, SST-Service 8%, Exempt).
/// Maps to ERPNext accounts/doctype/tax_category.
/// </summary>
public class TaxCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; set; }
    public TaxType TaxType { get; set; }
    public bool IsActive { get; set; } = true;

    protected TaxCategory() { }

    public TaxCategory(Guid id, string code, string name, TaxType taxType, Guid? tenantId = null)
        : base(id)
    {
        SetCode(code);
        SetName(name);
        TaxType = taxType;
        TenantId = tenantId;
    }

    public void SetCode(string code)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), TaxCategoryConsts.MaxCodeLength);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), TaxCategoryConsts.MaxNameLength);
    }
}
