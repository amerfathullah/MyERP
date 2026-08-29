using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Bill of Materials — defines raw materials needed to manufacture a finished item.
/// </summary>
public class BillOfMaterials : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string BomNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string? Uom { get; set; }

    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }

    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }

    public decimal TotalMaterialCost { get; set; }
    public decimal OperatingCost { get; set; }

    /// <summary>
    /// Portion of TotalMaterialCost allocated away to scrap/by-product secondary items —
    /// that raw-material spend produces the secondary item's own stock value, not the
    /// finished good's, so it must not also count toward the FG's cost (per ERPNext
    /// bom/services/costing.py: total_cost = operating_cost + raw_material_cost -
    /// secondary_items_cost). Mirrors the per-item allocation RecalculateCost() applies.
    /// </summary>
    public decimal SecondaryItemsCost => SecondaryItems
        .Where(s => s.CostAllocationPercentage > 0)
        .Sum(s => TotalMaterialCost * (s.CostAllocationPercentage / 100m));

    public decimal TotalCost => TotalMaterialCost + OperatingCost - SecondaryItemsCost;

    /// <summary>Whether this BOM defines routing operations.</summary>
    public bool WithOperations { get; set; }

    /// <summary>
    /// Transfer material against: "Work Order" or "Job Card".
    /// Per gotcha #446: forced to "Work Order" when WithOperations is false.
    /// </summary>
    public string TransferMaterialAgainst { get; set; } = "Work Order";

    /// <summary>Whether semi-finished goods tracking is enabled across operations.</summary>
    public bool TrackSemiFinishedGoods { get; set; }

    /// <summary>
    /// Per-BOM override for backflush method. "BOM" or "Material Transferred for Manufacture".
    /// When set, takes precedence over ManufacturingSettings global value.
    /// Per DO-NOT: "Skip per-BOM backflush_based_on override"
    /// </summary>
    public string? BackflushBasedOn { get; set; }

    /// <summary>Routing reference for operations sequencing.</summary>
    public Guid? RoutingId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Process loss percentage at BOM level (overall production loss).
    /// Per gotcha #442: TWO calculations — BOM-level AND per-secondary-item.
    /// BOM-level: process_loss_qty = quantity × (process_loss_percentage / 100).
    /// </summary>
    public decimal ProcessLossPercentage { get; set; }

    /// <summary>Process loss quantity derived from BOM-level percentage.</summary>
    public decimal ProcessLossQty => Quantity * (ProcessLossPercentage / 100m);

    /// <summary>Scrap/secondary items target warehouse.</summary>
    public Guid? ScrapWarehouseId { get; set; }

    public List<BomItem> Items { get; private set; } = new();
    public List<BomOperation> Operations { get; private set; } = new();
    public List<BomSecondaryItem> SecondaryItems { get; private set; } = new();

    protected BillOfMaterials() { }

    public BillOfMaterials(Guid id, Guid companyId, string bomNumber, Guid itemId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        BomNumber = bomNumber;
        ItemId = itemId;
        TenantId = tenantId;
    }

    public void RecalculateCost()
    {
        TotalMaterialCost = 0;
        foreach (var item in Items)
        {
            item.Recalculate();
            TotalMaterialCost += item.Amount;
        }
        OperatingCost = Operations.Sum(o => o.OperatingCost);

        // Distribute cost to secondary items based on their allocation percentage
        foreach (var si in SecondaryItems.Where(s => s.CostAllocationPercentage > 0))
        {
            // Per gotcha #518: item.cost_allocation = raw_material_cost × (pct / 100)
            var allocatedCost = TotalMaterialCost * (si.CostAllocationPercentage / 100m);
            if (si.EffectiveQuantity > 0)
                si.Rate = allocatedCost / si.EffectiveQuantity;
        }
    }

    /// <summary>
    /// Add an operation to this BOM. Validates monotonically non-decreasing sequence.
    /// Per ERPNext: same sequence_id = parallel operations (allowed).
    /// Per DO-NOT: "Allow routing sequence_id to decrease between rows"
    /// </summary>
    public void ValidateOperations()
    {
        if (!WithOperations)
        {
            TransferMaterialAgainst = "Work Order";
        }
        else if (string.IsNullOrWhiteSpace(TransferMaterialAgainst) && !TrackSemiFinishedGoods)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Transfer Material Against is mandatory when With Operations is enabled.");
        }

        // Per ERPNext PR #57885 / commit 3497a6a6bf:
        // When TrackSemiFinishedGoods is enabled, every operation must specify a FinishedGoodItemId
        // (or defaults to BOM.ItemId if it's the final operation).
        if (TrackSemiFinishedGoods && Operations.Any())
        {
            var maxSeq = Operations.Max(o => o.SequenceId);
            foreach (var op in Operations)
            {
                if (!op.FinishedGoodItemId.HasValue)
                {
                    if (op.SequenceId == maxSeq || op.IsFinalFinishedGood)
                    {
                        op.FinishedGoodItemId = ItemId;
                    }
                    else
                    {
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"FG / Semi FG Item is required for operation (Sequence #{op.SequenceId}) when Track Semi Finished Goods is enabled.");
                    }
                }
            }
        }
    }

    public void AddOperation(BomOperation operation)
    {
        if (Operations.Any() && operation.SequenceId < Operations.Max(o => o.SequenceId))
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Operation sequence_id must be monotonically non-decreasing");
        Operations.Add(operation);
        WithOperations = true;
        ValidateOperations();
    }

    /// <summary>
    /// Adds a secondary item (co-product/by-product/scrap) to this BOM.
    /// Per DO-NOT: FG item CANNOT appear in secondary_items table.
    /// Per DO-NOT: process_loss_per must be less than 100%.
    /// </summary>
    public void AddSecondaryItem(BomSecondaryItem item)
    {
        if (item.ItemId == ItemId)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.BomFgCannotBeSecondaryItem)
                .WithData("itemId", item.ItemId);

        if (item.ProcessLossPercentage >= 100m)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidProcessLossPercentage)
                .WithData("percentage", item.ProcessLossPercentage);

        SecondaryItems.Add(item);
    }

    /// <summary>
    /// Validates that FG + all secondary items cost allocation totals exactly 100%.
    /// Per DO-NOT: "Skip FG cost_allocation_per validation (FG + all secondary items MUST total exactly 100%)"
    /// </summary>
    public bool ValidateCostAllocation()
    {
        if (!SecondaryItems.Any(si => si.CostAllocationPercentage > 0))
            return true; // No cost allocation configured — FG gets 100% implicitly

        var secondaryTotal = SecondaryItems.Sum(si => si.CostAllocationPercentage);
        var fgAllocation = 100m - secondaryTotal;
        return fgAllocation >= 0 && secondaryTotal <= 100m;
    }

    /// <summary>
    /// Gets the FG cost allocation percentage (auto-reduced when secondary items have allocation).
    /// Per gotcha #518: FG's allocation = 100 - total_secondary_pct.
    /// </summary>
    public decimal FgCostAllocationPercentage
    {
        get
        {
            var secondaryTotal = SecondaryItems.Sum(si => si.CostAllocationPercentage);
            return 100m - secondaryTotal;
        }
    }
}
