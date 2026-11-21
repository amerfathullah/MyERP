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
    /// Excludes legacy items (!s.IsLegacy) per ERPNext PR #58979.
    /// </summary>
    public decimal SecondaryItemsCost => SecondaryItems
        .Where(s => !s.IsLegacy && s.CostAllocationPercentage > 0)
        .Sum(s => TotalMaterialCost * (s.CostAllocationPercentage / 100m));

    public decimal TotalCost => TotalMaterialCost + OperatingCost - SecondaryItemsCost;

    /// <summary>Whether this BOM defines routing operations.</summary>
    public bool WithOperations { get; set; }

    /// <summary>Whether this BOM is a phantom BOM (not stocked independently; ERPNext bom.py is_phantom_bom).</summary>
    public bool IsPhantomBom { get; set; }

    /// <summary>
    /// Whether operating cost is derived from finished-good BOM quantity rather than routing operations
    /// (ERPNext bom.py fg_based_operating_cost &amp; commit 7fb16ab015).
    /// </summary>
    public bool FgBasedOperatingCost { get; set; }

    /// <summary>Operating cost per unit of finished good when FgBasedOperatingCost is enabled.</summary>
    public decimal OperatingCostPerBomQuantity { get; set; }

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

    /// <summary>
    /// Effective good output quantity after deducting BOM-level process loss (ERPNext PR #60561 / commit 9167d69a84).
    /// Falls back to Quantity (or 1) if process loss is 100% or Quantity is 0.
    /// </summary>
    public decimal GoodQuantity => (Quantity - ProcessLossQty) > 0 ? (Quantity - ProcessLossQty) : (Quantity > 0 ? Quantity : 1m);

    /// <summary>
    /// Cost per good output unit = TotalCost / (Quantity - ProcessLossQty) per ERPNext PR #60561.
    /// </summary>
    public decimal UnitCost => GoodQuantity > 0 ? TotalCost / GoodQuantity : TotalCost;

    /// <summary>Scrap/secondary items target warehouse.</summary>
    public Guid? ScrapWarehouseId { get; set; }

    /// <summary>Set component quantities based on percentage formulation (ERPNext commit d07f4bb857).</summary>
    public bool SetQtyBasedOnPercentage { get; set; }

    /// <summary>
    /// Finished good cost allocation percentage. Maps to ERPNext bom.py cost_allocation_per.
    /// Alias for FgCostAllocationPercentage per PR #58939 / commit 33a066d568.
    /// </summary>
    public decimal CostAllocationPercentage
    {
        get => FgCostAllocationPercentage;
        set => FgCostAllocationPercentage = value;
    }

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

    /// <summary>
    /// Computes component quantities from formulation percentages.
    /// Per ERPNext commit d07f4bb857:
    /// - Mutually exclusive with TrackSemiFinishedGoods.
    /// - Validates at most one balance item.
    /// - Automatically sets balance item percentage = 100 - sum(others).
    /// - Requires total percentage to be exactly 100%.
    /// - Computes row.Quantity = (Percentage / 100) * Quantity.
    /// </summary>
    public void SetQtyFromPercentage()
    {
        if (!SetQtyBasedOnPercentage || !Items.Any()) return;

        if (TrackSemiFinishedGoods)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "'Set Component Quantities Based On Percentage' cannot be used together with 'Track Semi Finished Goods'.");
        }

        foreach (var row in Items)
        {
            if (row.Percentage <= 0 && !row.IsBalanceItem)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Percentage is required for item {row.ItemName} as 'Set Component Quantities Based On Percentage' is enabled.");
            }
        }

        var balanceRows = Items.Where(r => r.IsBalanceItem).ToList();
        if (balanceRows.Count > 1)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Only one component can be marked as Balance Item.");
        }

        if (balanceRows.Count == 1)
        {
            var nonBalanceSum = Items.Where(r => !r.IsBalanceItem).Sum(r => r.Percentage);
            if (nonBalanceSum >= 100m)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"The other components already total {nonBalanceSum}%, so no percentage remains for the Balance Item {balanceRows[0].ItemName}.");
            }
            balanceRows[0].Percentage = 100m - nonBalanceSum;
        }

        var totalPercentage = Items.Sum(r => r.Percentage);
        if (Math.Abs(totalPercentage - 100m) > 0.0001m)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The percentages of the components must total 100%. The current total is {totalPercentage}%.");
        }

        foreach (var row in Items)
        {
            row.Quantity = (row.Percentage / 100m) * Quantity;
            row.Recalculate();
        }
    }

    public void RecalculateCost()
    {
        SetQtyFromPercentage();
        TotalMaterialCost = 0;
        foreach (var item in Items)
        {
            item.Recalculate();
            TotalMaterialCost += item.Amount;
        }
        if (WithOperations || Operations.Any())
        {
            // Per ERPNext commits 22d504f5b6, e7be233789, f406f6e2b8: sum unrounded operation costs
            OperatingCost = Operations.Sum(o => o.GetEffectiveOperatingCost(Quantity));
        }
        else if (FgBasedOperatingCost && !IsPhantomBom)
        {
            OperatingCost = Quantity * OperatingCostPerBomQuantity;
        }
        else
        {
            OperatingCost = 0m;
        }

        // Valuation Rate and Manual rows carry their own cost, deducted from raw material cost;
        // The PercentageOfComponentCost rows split the remaining allocation basis by their percentage (per ERPNext PR #58431 / PR #59021).
        var ownCost = SecondaryItems
            .Where(si => si.CostAllocationPercentage == 0 && (si.ValuationType == SecondaryItemValuationType.ValuationRate || si.ValuationType == SecondaryItemValuationType.Manual))
            .Sum(si => si.Amount);

        var allocationBasis = Math.Max(0, TotalMaterialCost - ownCost);

        foreach (var si in SecondaryItems.Where(s => !s.IsLegacy && (s.CostAllocationPercentage > 0 || s.ValuationType == SecondaryItemValuationType.PercentageOfComponentCost)))
        {
            if (si.ValuationType != SecondaryItemValuationType.PercentageOfComponentCost)
            {
                si.CostAllocationPercentage = 0;
            }

            if (si.CostAllocationPercentage > 0)
            {
                var allocatedCost = allocationBasis * (si.CostAllocationPercentage / 100m);
                if (si.EffectiveQuantity > 0)
                    si.Rate = allocatedCost / si.EffectiveQuantity;
            }
        }

        var totalSecondaryAllocation = SecondaryItems
            .Where(s => !s.IsLegacy && s.ValuationType == SecondaryItemValuationType.PercentageOfComponentCost)
            .Sum(s => s.CostAllocationPercentage);
        CostAllocationPercentage = Math.Max(0, 100m - totalSecondaryAllocation);
    }

    /// <summary>
    /// Computes the cost and base-currency cost of every non-legacy secondary item row,
    /// along with the BOM's total secondary items cost in transaction and base currency.
    /// Per ERPNext commit a636da6cdd (calculate_secondary_items_cost):
    /// - Valuation Rate and Manual rows round their own cost to <paramref name="costPrecision"/>
    ///   and also compute their base cost (<c>cost * conversionRate</c>).
    /// - Percentage of Component Cost rows split <c>TotalMaterialCost - ownCost</c> by their allocation percentage.
    /// </summary>
    public (
        decimal SecondaryItemsCost,
        decimal BaseSecondaryItemsCost,
        IReadOnlyList<(Guid Id, decimal Cost, decimal BaseCost)> RowCosts)
        CalculateSecondaryItemsCostSummary(decimal conversionRate = 1m, int costPrecision = 2)
    {
        var rate = conversionRate > 0m ? conversionRate : 1m;
        var activeRows = SecondaryItems.Where(s => !s.IsLegacy).ToList();

        static bool HasOwnCost(BomSecondaryItem row) =>
            row.ValuationType is SecondaryItemValuationType.ValuationRate or SecondaryItemValuationType.Manual;

        var ownCost = activeRows
            .Where(HasOwnCost)
            .Sum(r => Math.Round(r.Amount, costPrecision, MidpointRounding.AwayFromZero));

        var allocationBasis = TotalMaterialCost - ownCost;

        decimal total = 0m;
        decimal baseTotal = 0m;
        var rowCosts = new List<(Guid Id, decimal Cost, decimal BaseCost)>(activeRows.Count);

        foreach (var row in activeRows)
        {
            var cost = HasOwnCost(row)
                ? Math.Round(row.Amount, costPrecision, MidpointRounding.AwayFromZero)
                : Math.Round((allocationBasis * row.CostAllocationPercentage) / 100m, costPrecision, MidpointRounding.AwayFromZero);
            var baseCost = Math.Round(cost * rate, costPrecision, MidpointRounding.AwayFromZero);

            total += cost;
            baseTotal += baseCost;
            rowCosts.Add((row.Id, cost, baseCost));
        }

        return (total, baseTotal, rowCosts);
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

        // Per ERPNext PR #53625 / commit 7f70e62c30:
        // Operation time should be greater than 0 for operations
        if (WithOperations && Operations.Any())
        {
            foreach (var op in Operations)
            {
                if (op.TimeInMins <= 0)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Operation time should be greater than 0 for operation (Sequence #{op.SequenceId}).");
                }
            }
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

    /// <summary>
    /// Adds a raw material / component to this BOM.
    /// Per ERPNext PR #47543 / commit 4241bfd4bc: Allows FG item to be used as RM by default (sets DoNotExplode = true).
    /// </summary>
    public void AddItem(BomItem item)
    {
        if (item.ItemId == ItemId)
        {
            item.DoNotExplode = true;
            item.SubBomId = null;
        }

        Items.Add(item);
    }

    public void AddOperation(BomOperation operation)
    {
        if (Operations.Any() && operation.SequenceId < Operations.Max(o => o.SequenceId))
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Operation sequence_id must be monotonically non-decreasing");
        if (!operation.TargetWarehouseId.HasValue && TargetWarehouseId.HasValue)
        {
            operation.TargetWarehouseId = TargetWarehouseId;
        }
        Operations.Add(operation);
        WithOperations = true;
        ValidateOperations();
    }

    /// <summary>
    /// Fills or clears operation target warehouses from BOM default TargetWarehouseId
    /// (per ERPNext commits aba50eb01d and 46c37b5eef).
    /// </summary>
    public void SyncOperationTargetWarehouses(Guid? defaultTargetWarehouseId)
    {
        TargetWarehouseId = defaultTargetWarehouseId;
        foreach (var op in Operations)
        {
            op.TargetWarehouseId = defaultTargetWarehouseId;
        }
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

        // Per ERPNext PR #59021 / bom.js: cost allocation % only applies to PercentageOfComponentCost
        if (item.ValuationType != SecondaryItemValuationType.PercentageOfComponentCost)
        {
            item.CostAllocationPercentage = 0;
        }
        else
        {
            item.ValuationType = SecondaryItemValuationType.PercentageOfComponentCost;
        }

        SecondaryItems.Add(item);
    }

    private decimal? _fgCostAllocationPercentage;

    /// <summary>
    /// Gets or sets the FG cost allocation percentage. Defaults to 100%.
    /// Maps directly to FgCostAllocationPercentage (persisted in EF Core).
    /// Auto-reduced when secondary items have cost allocation per ERPNext PR #58979 / PR #58939 / gotcha #518.
    /// </summary>
    public decimal FgCostAllocationPercentage
    {
        get
        {
            if (_fgCostAllocationPercentage.HasValue)
                return _fgCostAllocationPercentage.Value;

            var secondaryTotal = SecondaryItems.Where(si => !si.IsLegacy).Sum(si => si.CostAllocationPercentage);
            return Math.Clamp(100m - secondaryTotal, 0m, 100m);
        }
        set => _fgCostAllocationPercentage = value;
    }

    /// <summary>
    /// Sets FG cost allocation percentage.
    /// Per ERPNext PR #58979 / commit bc2fa03730 and PR #58939 / commit 33a066d568:
    /// Handles empty/float coercion, and if FG allocation is 100% while secondary items exist,
    /// auto-reduces FG allocation: cost_allocation_per = 100 - total_secondary_items_per.
    /// </summary>
    public void SetFgCostAllocation(decimal? costAllocationPercentage = null)
    {
        if (costAllocationPercentage.HasValue)
            _fgCostAllocationPercentage = costAllocationPercentage.Value;

        var totalSecondary = SecondaryItems.Where(s => !s.IsLegacy).Sum(s => s.CostAllocationPercentage);
        var currentFg = _fgCostAllocationPercentage ?? 100m;

        if (currentFg == 100m && totalSecondary > 0)
        {
            _fgCostAllocationPercentage = Math.Max(0m, 100m - totalSecondary);
        }
    }

    /// <summary>
    /// Validates that FG + all secondary items cost allocation totals exactly 100%.
    /// Per DO-NOT: "Skip FG cost_allocation_per validation (FG + all secondary items MUST total exactly 100%)"
    /// Per ERPNext PR #58979 / commit bc2fa03730: total_cost_allocation_per must equal 100%.
    /// </summary>
    public bool ValidateCostAllocation()
    {
        var secondaryTotal = SecondaryItems.Where(si => !si.IsLegacy).Sum(si => si.CostAllocationPercentage);
        if (secondaryTotal == 0 && (!_fgCostAllocationPercentage.HasValue || _fgCostAllocationPercentage.Value == 100m))
            return true; // No cost allocation configured — FG gets 100% implicitly

        var fgAllocation = FgCostAllocationPercentage;
        return fgAllocation >= 0 && Math.Abs((fgAllocation + secondaryTotal) - 100m) < 0.0001m;
    }

    /// <summary>
    /// Validates BOM process loss percentage is non-negative (per ERPNext PR #50629 / commit 1ee700fff3).
    /// </summary>
    public void ValidateProcessLoss()
    {
        if (ProcessLossPercentage < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Process loss percentage cannot be negative.");
        }
    }

    /// <summary>
    /// Validates that <c>WithOperations</c> and <c>FgBasedOperatingCost</c> are not both enabled on a BOM
    /// (per ERPNext commit 98fc122a1f).
    /// </summary>
    public void ValidateOperationsConfiguration()
    {
        if (WithOperations && FgBasedOperatingCost)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot set both 'With Operations' and 'FG Based Operating Cost' on a BOM.");
        }
    }

    /// <summary>
    /// Validates that total cost of secondary items does not exceed total raw material cost.
    /// Per ERPNext PR #58979 (commit 4d95a240bb / b9502c74cc):
    /// "The cost of the secondary items cannot exceed the raw material cost of {0}."
    /// </summary>
    public void ValidateSecondaryItemsCost()
    {
        var secondaryItemsCost = SecondaryItems
            .Where(si => !si.IsLegacy)
            .Sum(si => si.EffectiveQuantity * si.Rate);

        if (secondaryItemsCost > TotalMaterialCost && TotalMaterialCost > 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The cost of the secondary items ({secondaryItemsCost:F2}) cannot exceed the raw material cost of {TotalMaterialCost:F2}.");
        }
    }

    /// <summary>
    /// Computes BOM base cost totals rounded to field precision and detects whether any BOM item row
    /// or parent BOM needs updating per ERPNext commits 42a3a8342d, 2dde60cc73, 83824c5562, 98fc122a1f,
    /// 08cee5f68b, and 6b3749cd66 (<c>bom/services/costing.py</c>).
    /// </summary>
    public static (
        decimal BaseOperatingCost,
        decimal BaseRawMaterialCost,
        decimal TotalCost,
        decimal BaseTotalCost,
        bool ShouldUpdateParentBoms,
        List<(Guid ItemRowId, decimal Amount, decimal BaseAmount, bool ShouldSaveRow)> ItemResults)
        CalculateBomBaseCostTotals(
            decimal conversionRate,
            decimal existingTotalCost,
            decimal existingBaseTotalCost,
            decimal operatingCost,
            decimal baseOperatingCostUnrounded,
            decimal secondaryItemsCost,
            decimal baseSecondaryItemsCost,
            IReadOnlyList<(Guid ItemRowId, decimal OldRate, decimal NewRate, decimal Qty, decimal OldBaseAmount)> items,
            int currencyPrecision = 2)
    {
        var fx = conversionRate > 0m ? conversionRate : 1m;
        var itemResults = new List<(Guid ItemRowId, decimal Amount, decimal BaseAmount, bool ShouldSaveRow)>();
        decimal rawMaterialCost = 0m;
        decimal baseRawMaterialCostUnrounded = 0m;

        if (items != null)
        {
            foreach (var item in items)
            {
                var amount = Math.Round(
                    Math.Round(item.NewRate, currencyPrecision, MidpointRounding.AwayFromZero) * item.Qty,
                    currencyPrecision,
                    MidpointRounding.AwayFromZero);
                var baseAmount = Math.Round(amount * fx, currencyPrecision, MidpointRounding.AwayFromZero);
                var shouldSave = item.OldRate != item.NewRate || item.OldBaseAmount != baseAmount;

                itemResults.Add((item.ItemRowId, amount, baseAmount, shouldSave));
                rawMaterialCost += amount;
                baseRawMaterialCostUnrounded += baseAmount;
            }
        }

        var baseOperatingCost = Math.Round(baseOperatingCostUnrounded, currencyPrecision, MidpointRounding.AwayFromZero);
        var baseRawMaterialCost = Math.Round(baseRawMaterialCostUnrounded, currencyPrecision, MidpointRounding.AwayFromZero);
        var totalCost = operatingCost + rawMaterialCost - secondaryItemsCost;
        var baseTotalCost = Math.Round(
            baseOperatingCost + baseRawMaterialCost - baseSecondaryItemsCost,
            currencyPrecision,
            MidpointRounding.AwayFromZero);

        var shouldUpdateParents = (totalCost, baseTotalCost) != (existingTotalCost, existingBaseTotalCost);
        return (baseOperatingCost, baseRawMaterialCost, totalCost, baseTotalCost, shouldUpdateParents, itemResults);
    }

    /// <summary>
    /// Splits a Manufacture Stock Entry's operation cost across workstation components in table order using
    /// running-total rounding, recording a row for every positive-rate component (even when its rounded share is 0)
    /// with <paramref name="entryConsumedQty"/> per ERPNext commits 1bc6b681ff, 21589f031f, 6688a76b2e,
    /// b6048239c8, 8085efd1f8, bba6fcb0d6, and f868ded52f (<c>bom/services/operations_cost.py</c>).
    /// </summary>
    public static (bool AddedComponentRows, List<(string OperatingComponent, Guid ExpenseAccountId, decimal Amount, decimal ConsumedQty)> ComponentRows)
        SplitOperationCostByWorkstationComponents(
            decimal operationActualOperatingCost,
            decimal entryOperationAmount,
            decimal entryConsumedQty,
            Guid fallbackExpenseAccountId,
            IReadOnlyList<(string OperatingComponent, decimal ComponentRate, Guid? ComponentExpenseAccountId)>? workstationComponentsInTableOrder,
            int amountPrecision = 2)
    {
        var rows = new List<(string OperatingComponent, Guid ExpenseAccountId, decimal Amount, decimal ConsumedQty)>();
        if (workstationComponentsInTableOrder == null)
        {
            return (false, rows);
        }

        var positiveComponents = workstationComponentsInTableOrder
            .Where(c => c.ComponentRate > 0m)
            .ToList();
        var totalComponentRate = positiveComponents.Sum(c => c.ComponentRate);

        if (operationActualOperatingCost <= 0m || totalComponentRate <= 0m || entryOperationAmount <= 0m)
        {
            return (false, rows);
        }

        decimal allocated = 0m;
        decimal cumulativeRate = 0m;

        foreach (var wc in positiveComponents)
        {
            cumulativeRate += wc.ComponentRate;
            var cumulativeAmount = Math.Round(
                entryOperationAmount * cumulativeRate / totalComponentRate,
                amountPrecision,
                MidpointRounding.AwayFromZero);
            var share = Math.Round(cumulativeAmount - allocated, amountPrecision, MidpointRounding.AwayFromZero);
            allocated = cumulativeAmount;

            var accountId = (wc.ComponentExpenseAccountId.HasValue && wc.ComponentExpenseAccountId.Value != Guid.Empty)
                ? wc.ComponentExpenseAccountId.Value
                : fallbackExpenseAccountId;
            rows.Add((wc.OperatingComponent, accountId, share, entryConsumedQty));
        }

        return (true, rows);
    }

    /// <summary>
    /// Blocks deactivating or cancelling a BOM when it is used as <c>finished_good_bom</c> in an active
    /// Subcontracting BOM per ERPNext commit f10219955d (<c>bom.py</c>).
    /// </summary>
    public static void ValidateNotUsedByActiveSubcontractingBom(string? activeSubcontractingBomName)
    {
        if (!string.IsNullOrWhiteSpace(activeSubcontractingBomName))
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot deactivate or cancel BOM as it is used in the active Subcontracting BOM {activeSubcontractingBomName}");
        }
    }

    /// <summary>
    /// Normalizes a material row copied from an operation BOM to stock UOM (<c>ConversionFactor = 1</c>,
    /// <c>DoNotExplode = true</c>) per ERPNext commit 17cbca89c1 (<c>_append_bom_material_row</c> in <c>bom.py</c>).
    /// </summary>
    public static (string Uom, decimal ConversionFactor, bool DoNotExplode) NormalizeOperationBomMaterialRow(string stockUom)
    {
        return (stockUom, 1m, true);
    }
}
