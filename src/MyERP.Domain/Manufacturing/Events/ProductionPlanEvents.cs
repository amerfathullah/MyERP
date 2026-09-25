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

public class ProductionPlanClosedEvent
{
    public Guid ProductionPlanId { get; }
    public Guid? TenantId { get; }

    public ProductionPlanClosedEvent(Guid productionPlanId, Guid? tenantId)
    {
        ProductionPlanId = productionPlanId;
        TenantId = tenantId;
    }
}

public class ProductionPlanReopenedEvent
{
    public Guid ProductionPlanId { get; }
    public Guid? TenantId { get; }

    public ProductionPlanReopenedEvent(Guid productionPlanId, Guid? tenantId)
    {
        ProductionPlanId = productionPlanId;
        TenantId = tenantId;
    }
}

public class ProductionPlanCompletedEvent
{
    public Guid ProductionPlanId { get; }
    public Guid? TenantId { get; }

    public ProductionPlanCompletedEvent(Guid productionPlanId, Guid? tenantId)
    {
        ProductionPlanId = productionPlanId;
        TenantId = tenantId;
    }
}

public class ProductionPlanCompletionRevertedEvent
{
    public Guid ProductionPlanId { get; }
    public Guid? TenantId { get; }

    public ProductionPlanCompletionRevertedEvent(Guid productionPlanId, Guid? tenantId)
    {
        ProductionPlanId = productionPlanId;
        TenantId = tenantId;
    }
}

