using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Incoterm — standardized international trade shipping term (e.g. "FOB", "CIF", "EXW").
/// Referenced by Quotation, Sales Order, and Purchase Order to record who bears
/// shipping cost/risk at each stage. Maps to ERPNext setup/doctype/incoterm.
/// </summary>
public class Incoterm : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Standard 3-letter code (e.g. "FOB", "CIF", "EXW", "DDP").</summary>
    public string Code { get; private set; } = null!;

    public string Title { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected Incoterm() { }

    public Incoterm(Guid id, string code, string title, Guid? tenantId = null) : base(id)
    {
        SetCode(code);
        SetTitle(title);
        TenantId = tenantId;
    }

    public void SetCode(string code)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), 10).ToUpperInvariant();
    }

    public void SetTitle(string title)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 200);
    }
}
