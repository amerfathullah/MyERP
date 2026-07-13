using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Contact — person associated with a Customer, Supplier, Company, or other entity.
/// Uses same PartyType/PartyId pattern as Address for polymorphic linking.
/// </summary>
public class Contact : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Salutation { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }

    /// <summary>Entity type this contact belongs to: Customer, Supplier, Company.</summary>
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }

    /// <summary>Primary contact for billing/correspondence.</summary>
    public bool IsPrimaryContact { get; set; }

    /// <summary>Contact for billing/invoicing.</summary>
    public bool IsBillingContact { get; set; }

    public bool IsActive { get; set; } = true;

    protected Contact() { }

    public Contact(Guid id, string firstName, string partyType, Guid partyId, Guid? tenantId = null)
        : base(id)
    {
        FirstName = firstName;
        PartyType = partyType;
        PartyId = partyId;
        TenantId = tenantId;
    }

    public string FullName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";
}
