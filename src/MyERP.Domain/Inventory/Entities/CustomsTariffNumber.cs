using System;
using MyERP.Inventory;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Customs Tariff Number (HS Code / Tariff Code) for import/export declaration.
/// Maps to ERPNext stock/doctype/customs_tariff_number.
/// </summary>
public class CustomsTariffNumber : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string TariffNumber { get; private set; } = null!;
    public string? Description { get; set; }

    protected CustomsTariffNumber() { }

    public CustomsTariffNumber(
        Guid id,
        Guid companyId,
        string tariffNumber,
        string? description = null,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetTariffNumber(tariffNumber);
        Description = description;
        TenantId = tenantId;
    }

    public void SetTariffNumber(string tariffNumber)
    {
        TariffNumber = Check.NotNullOrWhiteSpace(
            tariffNumber,
            nameof(tariffNumber),
            CustomsTariffNumberConsts.MaxTariffNumberLength);
    }
}
