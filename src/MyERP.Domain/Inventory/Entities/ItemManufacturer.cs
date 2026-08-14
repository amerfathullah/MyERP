using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

public class ItemManufacturer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid ItemId { get; set; }
    public Guid ManufacturerId { get; set; }
    public string ManufacturerPartNo { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }

    protected ItemManufacturer() { }

    public ItemManufacturer(Guid id, Guid companyId, Guid itemId, Guid manufacturerId, string manufacturerPartNo, bool isDefault = false)
        : base(id)
    {
        CompanyId = companyId;
        ItemId = itemId;
        ManufacturerId = manufacturerId;
        SetManufacturerPartNo(manufacturerPartNo);
        IsDefault = isDefault;
    }

    public void SetManufacturerPartNo(string partNo)
    {
        Check.NotNullOrWhiteSpace(partNo, nameof(partNo));
        if (partNo.Length > ItemManufacturerConsts.MaxManufacturerPartNoLength)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("field", nameof(ManufacturerPartNo))
                .WithData("maxLength", ItemManufacturerConsts.MaxManufacturerPartNoLength);
        }
        ManufacturerPartNo = partNo.Trim();
    }
}
