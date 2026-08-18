using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Subcontracting BOM — maps a finished good to the subcontractor's service item, so
/// Subcontracting Order/Receipt creation can auto-populate the service line from the
/// finished good being ordered. Maps to ERPNext subcontracting/doctype/subcontracting_bom.
/// </summary>
public class SubcontractingBom : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid FinishedGoodId { get; set; }
    public decimal FinishedGoodQty { get; set; } = 1;
    public Guid FinishedGoodBomId { get; set; }
    public string? FinishedGoodUom { get; set; }

    public Guid ServiceItemId { get; set; }
    public decimal ServiceItemQty { get; set; } = 1;
    public string? ServiceItemUom { get; set; }

    /// <summary>service_item_qty / finished_good_qty — recomputed via <see cref="SetConversionFactor"/>.</summary>
    public decimal ConversionFactor { get; private set; } = 1;

    protected SubcontractingBom() { }

    public SubcontractingBom(Guid id, Guid finishedGoodId, decimal finishedGoodQty, Guid finishedGoodBomId,
        Guid serviceItemId, decimal serviceItemQty, Guid? tenantId = null) : base(id)
    {
        FinishedGoodId = Check.NotDefaultOrNull<Guid>(finishedGoodId, nameof(finishedGoodId));
        FinishedGoodQty = finishedGoodQty;
        FinishedGoodBomId = Check.NotDefaultOrNull<Guid>(finishedGoodBomId, nameof(finishedGoodBomId));
        ServiceItemId = Check.NotDefaultOrNull<Guid>(serviceItemId, nameof(serviceItemId));
        ServiceItemQty = serviceItemQty;
        TenantId = tenantId;
        SetConversionFactor();
    }

    public void Update(Guid finishedGoodId, decimal finishedGoodQty, Guid finishedGoodBomId, Guid serviceItemId, decimal serviceItemQty, bool isActive)
    {
        FinishedGoodId = finishedGoodId;
        FinishedGoodQty = finishedGoodQty;
        FinishedGoodBomId = finishedGoodBomId;
        ServiceItemId = serviceItemId;
        ServiceItemQty = serviceItemQty;
        IsActive = isActive;
        SetConversionFactor();
    }

    /// <summary>Per ERPNext set_conversion_factor(): conversion_factor = service_item_qty / finished_good_qty.</summary>
    private void SetConversionFactor()
    {
        if (FinishedGoodQty <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.SubcontractingBomInvalidQty);
        ConversionFactor = ServiceItemQty / FinishedGoodQty;
    }
}
