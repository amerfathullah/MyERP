using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Party Type — maps a party doctype (Customer, Supplier, Employee, ...) to its
/// ledger account type (Receivable/Payable) for GL Entry and Payment Entry party resolution.
/// Maps to ERPNext setup/doctype/party_type.
/// </summary>
public class PartyType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public PartyAccountType AccountType { get; set; }

    protected PartyType() { }

    public PartyType(Guid id, string name, PartyAccountType accountType, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: PartyTypeConsts.MaxPartyTypeNameLength);
        AccountType = accountType;
        TenantId = tenantId;
    }
}
