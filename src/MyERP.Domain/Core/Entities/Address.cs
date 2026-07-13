using System;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Address — shared entity linked to Customer, Supplier, Company, or Employee.
/// Uses PartyType/PartyId pattern (ERPNext Dynamic Link equivalent).
/// Multiple addresses per party supported with primary/shipping flags.
/// </summary>
public class Address : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Title/label (e.g., "Head Office", "Warehouse", "Billing").</summary>
    public string Title { get; set; } = null!;

    /// <summary>Type: Billing, Shipping, Office, Personal, Plant, Postal, Shop, Subsidiary, Warehouse, Current, Permanent, Other.</summary>
    public string AddressType { get; set; } = "Billing";

    public string AddressLine1 { get; set; } = null!;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Malaysia";

    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }

    /// <summary>Entity type this address belongs to: Customer, Supplier, Company, Employee.</summary>
    public string PartyType { get; set; } = null!;

    /// <summary>ID of the parent entity.</summary>
    public Guid PartyId { get; set; }

    /// <summary>Primary address for this party (used for billing by default).</summary>
    public bool IsPrimaryAddress { get; set; }

    /// <summary>Shipping address for this party.</summary>
    public bool IsShippingAddress { get; set; }

    /// <summary>Whether this address is disabled.</summary>
    public bool IsDisabled { get; set; }

    protected Address() { }

    public Address(Guid id, string title, string partyType, Guid partyId,
        string addressLine1, string country, Guid? tenantId = null)
        : base(id)
    {
        Title = title;
        PartyType = partyType;
        PartyId = partyId;
        AddressLine1 = addressLine1;
        Country = country;
        TenantId = tenantId;
    }

    /// <summary>Full address string for display.</summary>
    public string GetFullAddress()
    {
        var parts = new[] { AddressLine1, AddressLine2, City, State, PostalCode, Country };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
