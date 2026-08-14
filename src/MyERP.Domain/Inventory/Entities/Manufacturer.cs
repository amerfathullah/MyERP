using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

public class Manufacturer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ShortName { get; private set; } = null!;
    public string? FullName { get; set; }
    public string? Website { get; set; }
    public string? Country { get; set; }
    public string? LogoUrl { get; set; }
    public string? Notes { get; set; }

    protected Manufacturer() { }

    public Manufacturer(Guid id, Guid companyId, string shortName, string? fullName = null)
        : base(id)
    {
        CompanyId = companyId;
        SetShortName(shortName);
        FullName = fullName;
    }

    public void SetShortName(string shortName)
    {
        Check.NotNullOrWhiteSpace(shortName, nameof(shortName));
        if (shortName.Length > ManufacturerConsts.MaxShortNameLength)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("field", nameof(ShortName))
                .WithData("maxLength", ManufacturerConsts.MaxShortNameLength);
        }
        ShortName = shortName.Trim();
    }
}
