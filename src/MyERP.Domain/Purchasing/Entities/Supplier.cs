using System;
using MyERP.Purchasing;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Supplier master data.
/// Maps to ERPNext buying/doctype/supplier.
/// </summary>
public class Supplier : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; private set; } = null!;
    public string? SupplierCode { get; set; }

    /// <summary>Tax Identification Number — required for LHDN e-Invoice.</summary>
    public string? Tin { get; set; }

    /// <summary>Business registration number (BRN/SSM).</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>SST registration number.</summary>
    public string? SstRegistrationNumber { get; set; }

    /// <summary>ID type for LHDN (e.g., BRN, NRIC, PASSPORT, ARMY).</summary>
    public string? IdType { get; set; }

    /// <summary>ID value corresponding to IdType.</summary>
    public string? IdValue { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Address
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>Default payable account for this supplier.</summary>
    public Guid? DefaultPayableAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    protected Supplier() { }

    public Supplier(Guid id, Guid companyId, string name, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), SupplierConsts.MaxNameLength);
    }
}
