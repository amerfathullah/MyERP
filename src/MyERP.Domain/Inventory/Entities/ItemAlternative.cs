using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

public class ItemAlternative : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid ItemId { get; private set; }
    public Guid AlternativeItemId { get; private set; }
    public bool TwoWay { get; set; }

    protected ItemAlternative() { }

    public ItemAlternative(Guid id, Guid companyId, Guid itemId, Guid alternativeItemId, bool twoWay = false)
        : base(id)
    {
        CompanyId = companyId;
        SetItems(itemId, alternativeItemId);
        TwoWay = twoWay;
    }

    public void SetItems(Guid itemId, Guid alternativeItemId)
    {
        if (itemId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("field", nameof(ItemId));
        }

        if (alternativeItemId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("field", nameof(AlternativeItemId));
        }

        if (itemId == alternativeItemId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "Alternative item must not be same as base item");
        }

        ItemId = itemId;
        AlternativeItemId = alternativeItemId;
    }
}
