using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.CRM.Entities;

public class OpportunityItem : FullAuditedEntity<Guid>
{
    public Guid OpportunityId { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Uom { get; set; }

    protected OpportunityItem() { }

    public OpportunityItem(Guid id, Guid opportunityId, string description, decimal quantity, decimal unitPrice)
        : base(id)
    {
        OpportunityId = opportunityId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Amount = quantity * unitPrice;
    }

    public void Recalculate()
    {
        Amount = Quantity * UnitPrice;
    }
}
