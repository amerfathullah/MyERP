using System;

namespace MyERP.Purchasing.Events;

public class SubcontractingOrderClosedOrCancelledEvent
{
    public Guid SubcontractingOrderId { get; }
    public Guid? TenantId { get; }

    public SubcontractingOrderClosedOrCancelledEvent(Guid subcontractingOrderId, Guid? tenantId)
    {
        SubcontractingOrderId = subcontractingOrderId;
        TenantId = tenantId;
    }
}
