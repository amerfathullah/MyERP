using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Landed Cost Charge — individual charge line (freight, customs, insurance, etc.).
/// </summary>
public class LandedCostCharge : FullAuditedEntity<Guid>
{
    public Guid LandedCostVoucherId { get; set; }
    public string Description { get; set; } = null!;
    public Guid ExpenseAccountId { get; set; }
    public decimal Amount { get; set; }

    protected LandedCostCharge() { }

    public LandedCostCharge(Guid id, Guid landedCostVoucherId,
        string description, Guid expenseAccountId, decimal amount)
        : base(id)
    {
        LandedCostVoucherId = landedCostVoucherId;
        Description = description;
        ExpenseAccountId = expenseAccountId;
        Amount = amount;
    }
}
