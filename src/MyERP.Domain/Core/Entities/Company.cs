using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Represents a legal company entity. A tenant can have multiple companies.
/// Maps conceptually to ERPNext setup/doctype/company.
/// </summary>
public class Company : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? ShortName { get; set; }

    /// <summary>Tax Identification Number (TIN) — required for LHDN e-Invoice.</summary>
    public string? TaxId { get; set; }

    /// <summary>Company registration number (e.g., SSM in Malaysia).</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>SST registration number for Royal Malaysian Customs.</summary>
    public string? SstRegistrationNumber { get; set; }

    /// <summary>MSIC code for e-Invoice classification.</summary>
    public string? MsicCode { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Address
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>Default currency code (ISO 4217), e.g. "MYR".</summary>
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Fiscal year start month (1-12).</summary>
    public int FiscalYearStartMonth { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    protected Company() { } // EF Core constructor

    public Company(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), CompanyConsts.MaxNameLength);
    }
}
