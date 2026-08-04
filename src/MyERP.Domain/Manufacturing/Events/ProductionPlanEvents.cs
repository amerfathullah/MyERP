using System;

namespace MyERP.Manufacturing.Events;

public class ProductionPlanSubmittedEvent
{
    public Guid ProductionPlanId { get; }
    public Guid? TenantId { get; }

    public ProductionPlanSubmittedEvent(Guid productionPlanId, Guid? tenantId)
    {
        ProductionPlanId = productionPlanId;
        TenantId = tenantId;
    }
}

public class ProductionPlanCancelledEvent
{
    public Guid ProductionPlanId { get; }
    public Guid? TenantId { get; }

    public ProductionPlanCancelledEvent(Guid productionPlanId, Guid? tenantId)
    {
        ProductionPlanId = productionPlanId;
        TenantId = tenantId;
    }
}
