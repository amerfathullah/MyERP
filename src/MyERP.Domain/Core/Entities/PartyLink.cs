using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Links two parties for inter-company transactions (Customer ↔ Supplier).
/// Each party can appear in at most ONE link — enforced at DB level.
/// Per DO-NOT: "Allow Party Link where same party appears in multiple links (breaks inter-company)"
/// Maps to ERPNext accounts/doctype/party_link.
/// </summary>
public class PartyLink : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Primary party type (e.g., "Customer").</summary>
    public string PrimaryPartyType { get; set; } = null!;

    /// <summary>Primary party ID.</summary>
    public Guid PrimaryPartyId { get; set; }

    /// <summary>Secondary party type (e.g., "Supplier").</summary>
    public string SecondaryPartyType { get; set; } = null!;

    /// <summary>Secondary party ID.</summary>
    public Guid SecondaryPartyId { get; set; }

    protected PartyLink() { }

    public PartyLink(
        Guid id,
        string primaryPartyType,
        Guid primaryPartyId,
        string secondaryPartyType,
        Guid secondaryPartyId,
        Guid? tenantId = null) : base(id)
    {
        PrimaryPartyType = Check.NotNullOrWhiteSpace(primaryPartyType, nameof(primaryPartyType));
        PrimaryPartyId = Check.NotDefaultOrNull<Guid>(primaryPartyId, nameof(primaryPartyId));
        SecondaryPartyType = Check.NotNullOrWhiteSpace(secondaryPartyType, nameof(secondaryPartyType));
        SecondaryPartyId = Check.NotDefaultOrNull<Guid>(secondaryPartyId, nameof(secondaryPartyId));
        TenantId = tenantId;

        if (PrimaryPartyId == SecondaryPartyId && PrimaryPartyType == SecondaryPartyType)
            throw new BusinessException("MyERP:00005")
                .WithData("reason", "Cannot link a party to itself");
    }
}
