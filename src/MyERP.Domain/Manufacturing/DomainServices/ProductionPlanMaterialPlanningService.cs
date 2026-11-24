using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Source raw material requirement line before download/export processing.
/// </summary>
public class RawMaterialPlanRow
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StockUom { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal RequiredBomQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ActualQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ReservedQtyForProduction { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; } = 1m;
}

/// <summary>
/// Merged download row for Download Required Materials.
/// Per ERPNext PR #65049 (commits 243fff334d, fd4fbcf7ff, 5eabaab431, 872632bf14):
/// Includes UOM and merges transfer/purchase rows under the first row's UOM.
/// </summary>
public class DownloadRawMaterialRow
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StockUom { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public decimal RequiredBomQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ActualQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ReservedQtyForProduction { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal RequiredQty { get; set; }
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// Domain service for Production Plan raw material calculation and export.
/// </summary>
public class ProductionPlanMaterialPlanningService : DomainService
{
    public static readonly IReadOnlyList<string> DownloadHeaders = new[]
    {
        "Item Code",
        "Item Name",
        "Description",
        "Stock UOM",
        "Warehouse",
        "Required Qty as per BOM",
        "Projected Qty",
        "Available Qty In Hand",
        "Ordered Qty",
        "Planned Qty",
        "Reserved Qty for Production",
        "Safety Stock",
        "Required Qty",
        "UOM"
    };

    /// <summary>
    /// Builds raw material download rows with UOM and merges duplicate rows for the same item and warehouse.
    /// Per ERPNext PR #65049 (commits 243fff334d, fd4fbcf7ff, 5eabaab431, 872632bf14):
    /// - Includes "UOM" column representing the unit of Required Qty.
    /// - When duplicate rows for (ItemCode, Warehouse) exist (e.g. transfer row in Stock UOM and purchase row in Purchase UOM):
    ///   Merges subsequent rows into the first row's UOM by adding (stockQty / firstRow.ConversionFactor).
    ///   Rounds the merged Required Qty once to precision.
    /// </summary>
    public static List<DownloadRawMaterialRow> BuildDownloadRows(
        IEnumerable<RawMaterialPlanRow> items,
        int precision = 4)
    {
        var result = new List<DownloadRawMaterialRow>();
        var duplicateMap = new Dictionary<(Guid? ItemId, string ItemCode, string Warehouse), (DownloadRawMaterialRow Row, decimal ConversionFactor, decimal Quantity)>();

        foreach (var d in items)
        {
            var key = (d.ItemId, d.ItemCode.Trim(), d.Warehouse.Trim());
            var convFactor = d.ConversionFactor > 0m ? d.ConversionFactor : 1m;
            var stockQty = d.Quantity * convFactor;

            if (duplicateMap.TryGetValue(key, out var merged))
            {
                // Transfer rows in stock UOM, purchase rows in purchase UOM -> merge in first row's UOM
                merged.Quantity += stockQty / merged.ConversionFactor;
                merged.Row.RequiredQty = Math.Round(merged.Quantity, precision);
                merged.Row.RequiredBomQty = Math.Round(merged.Row.RequiredBomQty + d.RequiredBomQty, precision);
                duplicateMap[key] = merged;
                continue;
            }

            var downloadRow = new DownloadRawMaterialRow
            {
                ItemCode = d.ItemCode,
                ItemName = d.ItemName,
                Description = d.Description ?? string.Empty,
                StockUom = d.StockUom,
                Warehouse = d.Warehouse,
                RequiredBomQty = d.RequiredBomQty,
                ProjectedQty = d.ProjectedQty,
                ActualQty = d.ActualQty,
                OrderedQty = d.OrderedQty,
                PlannedQty = d.PlannedQty,
                ReservedQtyForProduction = d.ReservedQtyForProduction,
                SafetyStock = d.SafetyStock,
                RequiredQty = Math.Round(d.Quantity, precision),
                Uom = d.Uom
            };

            duplicateMap[key] = (downloadRow, convFactor, d.Quantity);
            result.Add(downloadRow);
        }

        return result;
    }

    /// <summary>
    /// Calculates Material Request item quantities for Production Plan when transferring from other warehouses.
    /// Per ERPNext PR #65145 (commits 1ca3aa000e, dbc1f9598c):
    /// - Takes the transfer quantity off the unrounded shortage before rounding to Purchase UOM.
    /// - For Material Transfer (isTransfer = true), the transfer quantity is NOT rounded to Purchase UOM whole numbers.
    /// - Only the remaining shortage for Purchase is rounded to Purchase UOM whole numbers.
    /// </summary>
    public static (decimal TransferQty, decimal PurchaseQty) CalculateTransferAndPurchaseQuantities(
        decimal totalShortage,
        decimal availableInOtherWarehouses,
        decimal purchaseConversionFactor = 1m,
        decimal minOrderQty = 0m,
        bool roundUpWholeUom = true)
    {
        var factor = purchaseConversionFactor > 0m ? purchaseConversionFactor : 1m;
        var transferStockQty = Math.Min(Math.Max(0m, totalShortage), Math.Max(0m, availableInOtherWarehouses));
        var remainingShortage = Math.Max(0m, totalShortage - transferStockQty);

        var purchaseInUom = remainingShortage / factor;
        if (minOrderQty > 0m && purchaseInUom > 0m && purchaseInUom < minOrderQty)
        {
            purchaseInUom = minOrderQty;
        }
        if (roundUpWholeUom)
        {
            purchaseInUom = CeilWholeUnit(purchaseInUom);
        }

        return (transferStockQty, purchaseInUom);
    }

    /// <summary>
    /// Rounds a quantity up to the next whole unit when <paramref name="mustBeWholeNumber"/> is true,
    /// pre-rounding to 9 decimal places so floating-point / repeating-decimal noise (e.g. 0.07 * 100 or
    /// 1 / 3 * 3) never adds a spurious extra unit per ERPNext PR #68803 and PR #67414.
    /// </summary>
    public static decimal CeilWholeUnit(decimal qty, bool mustBeWholeNumber = true)
    {
        if (!mustBeWholeNumber)
        {
            return qty;
        }

        return Math.Ceiling(Math.Round(qty, 9, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Shares a raw material row's RequiredBomQty across split transfer/purchase rows so it is reserved once.
    /// Per ERPNext PR #66580 (_split_required_bom_qty):
    /// Each leading split row receives min(row.Quantity, remaining), and the final row takes the remaining BOM qty.
    /// </summary>
    public static void SplitRequiredBomQty(
        decimal totalRequiredBomQty,
        IReadOnlyList<RawMaterialPlanRow> splitRows,
        int precision = 4)
    {
        if (splitRows.Count == 0)
        {
            return;
        }

        var remaining = totalRequiredBomQty;
        for (var i = 0; i < splitRows.Count - 1; i++)
        {
            var allocated = Math.Round(Math.Min(Math.Max(0m, splitRows[i].Quantity), Math.Max(0m, remaining)), precision);
            splitRows[i].RequiredBomQty = allocated;
            remaining -= allocated;
        }

        splitRows[^1].RequiredBomQty = Math.Round(Math.Max(0m, remaining), precision);
    }

    /// <summary>
    /// Splits a raw material requirement across transfer locations and a fallback purchase row,
    /// allocating RequiredBomQty once across the resulting rows.
    /// Per ERPNext PR #66580 (get_materials_from_other_locations).
    /// </summary>
    public static List<RawMaterialPlanRow> AllocateMaterialsFromOtherLocations(
        RawMaterialPlanRow item,
        IReadOnlyList<(string Warehouse, Guid? WarehouseId, decimal AvailableStockQty)> otherLocations,
        decimal purchaseConversionFactor = 1m,
        decimal minOrderQty = 0m,
        bool roundUpWholeUom = true,
        bool considerMinOrderQty = false,
        int precision = 4)
    {
        var factor = item.ConversionFactor > 0m ? item.ConversionFactor : 1m;
        var remainingStockQty = Math.Max(0m, item.Quantity * factor);
        var splitRows = new List<RawMaterialPlanRow>();

        foreach (var loc in otherLocations)
        {
            if (remainingStockQty <= 0m)
            {
                break;
            }

            // Per ERPNext PR #67488: skip the target warehouse itself so its stock is not counted as an external transfer source
            if (item.WarehouseId.HasValue && loc.WarehouseId == item.WarehouseId.Value)
            {
                continue;
            }

            if (loc.AvailableStockQty <= 0m)
            {
                continue;
            }

            var transferQty = Math.Min(remainingStockQty, loc.AvailableStockQty);
            remainingStockQty -= transferQty;

            splitRows.Add(new RawMaterialPlanRow
            {
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                StockUom = item.StockUom,
                Warehouse = loc.Warehouse,
                ItemId = item.ItemId,
                WarehouseId = loc.WarehouseId,
                ActualQty = loc.AvailableStockQty,
                SafetyStock = item.SafetyStock,
                Quantity = Math.Round(transferQty, precision),
                Uom = string.IsNullOrEmpty(item.StockUom) ? item.Uom : item.StockUom,
                ConversionFactor = 1m
            });
        }

        if (remainingStockQty > 0m)
        {
            var purchaseFactor = purchaseConversionFactor > 0m ? purchaseConversionFactor : 1m;
            var purchaseQty = remainingStockQty / purchaseFactor;
            if (considerMinOrderQty && minOrderQty > 0m && purchaseQty < minOrderQty)
            {
                purchaseQty = minOrderQty;
            }

            purchaseQty = roundUpWholeUom
                ? CeilWholeUnit(purchaseQty)
                : Math.Round(purchaseQty, precision);

            splitRows.Add(new RawMaterialPlanRow
            {
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                StockUom = item.StockUom,
                Warehouse = item.Warehouse,
                ItemId = item.ItemId,
                WarehouseId = item.WarehouseId,
                ActualQty = item.ActualQty,
                SafetyStock = item.SafetyStock,
                Quantity = purchaseQty,
                Uom = item.Uom,
                ConversionFactor = purchaseFactor
            });
        }

        SplitRequiredBomQty(item.RequiredBomQty, splitRows, precision);
        return splitRows;
    }

    /// <summary>
    /// Generates CSV content from download rows.
    /// </summary>
    public static string GenerateCsv(IEnumerable<DownloadRawMaterialRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", DownloadHeaders.Select(h => $"\"{h.Replace("\"", "\"\"")}\"")));

        foreach (var r in rows)
        {
            var values = new[]
            {
                r.ItemCode,
                r.ItemName,
                r.Description,
                r.StockUom,
                r.Warehouse,
                r.RequiredBomQty.ToString(CultureInfo.InvariantCulture),
                r.ProjectedQty.ToString(CultureInfo.InvariantCulture),
                r.ActualQty.ToString(CultureInfo.InvariantCulture),
                r.OrderedQty.ToString(CultureInfo.InvariantCulture),
                r.PlannedQty.ToString(CultureInfo.InvariantCulture),
                r.ReservedQtyForProduction.ToString(CultureInfo.InvariantCulture),
                r.SafetyStock.ToString(CultureInfo.InvariantCulture),
                r.RequiredQty.ToString(CultureInfo.InvariantCulture),
                r.Uom
            };

            sb.AppendLine(string.Join(",", values.Select(v => $"\"{v.Replace("\"", "\"\"")}\"")));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Resolves the lead time in days for an MRP row per ERPNext commit 1cae8b6982 (get_item_lead_time):
    /// - For Manufacture rows: returns 0 when requiredQty &lt;= 0; otherwise scales the per-unit manufacturing
    ///   lead time by requiredQty, rounds to 4 decimal places, and takes the ceiling.
    /// - For Purchase / Subcontract rows: looks up the purchase lead time for (itemId, defaultSupplierId)
    ///   first when defaultSupplierId is specified, falling back to the item's general purchase lead time
    ///   (without scaling by requiredQty).
    /// </summary>
    public static int ResolveMrpItemLeadTimeDays(
        Guid itemId,
        string typeOfMaterial,
        decimal requiredQty,
        Guid? defaultSupplierId = null,
        IReadOnlyDictionary<(Guid ItemId, Guid SupplierId), decimal>? supplierPurchaseLeadTimes = null,
        decimal itemPurchaseLeadTimeDays = 0m,
        decimal itemManufacturingLeadTimeDaysPerUnit = 0m)
    {
        decimal timeInDays;
        if (string.Equals(typeOfMaterial, "Manufacture", StringComparison.OrdinalIgnoreCase))
        {
            if (requiredQty <= 0m)
            {
                return 0;
            }

            timeInDays = Math.Round(itemManufacturingLeadTimeDaysPerUnit * requiredQty, 4, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (defaultSupplierId.HasValue &&
                defaultSupplierId.Value != Guid.Empty &&
                supplierPurchaseLeadTimes != null &&
                supplierPurchaseLeadTimes.TryGetValue((itemId, defaultSupplierId.Value), out var supplierLeadTime))
            {
                timeInDays = supplierLeadTime;
            }
            else
            {
                timeInDays = itemPurchaseLeadTimeDays;
            }
        }

        return (int)Math.Ceiling(timeInDays);
    }

    /// <summary>
    /// Aggregates bin availability for a Production Plan raw material shortage check per ERPNext commit 3b8085319d:
    /// - When itemWarehouseId is specified (e.g., item default warehouse), queries that receiving warehouse's bin
    ///   even if that warehouse was also listed in transferFromWarehouseIds, so existing stock and incoming quantities
    ///   in the receiving warehouse are not dropped and ordered twice.
    /// - When itemWarehouseId is null (availability spans all company warehouses), excludes transferFromWarehouseIds
    ///   so transfer-source bins are handled only in the subsequent transfer split pass.
    /// </summary>
    public static (decimal ActualQty, decimal ProjectedQty, decimal ReservedQtyForProduction, decimal OrderedQty, decimal PlannedQty) ResolveShortageBinDetails(
        Guid? itemWarehouseId,
        IReadOnlyCollection<Guid>? transferFromWarehouseIds,
        IEnumerable<(Guid WarehouseId, decimal ActualQty, decimal ProjectedQty, decimal ReservedQtyForProduction, decimal OrderedQty, decimal PlannedQty)> companyBins,
        Guid? itemGroupDefaultWarehouseId = null)
    {
        if (companyBins == null)
        {
            return (0m, 0m, 0m, 0m, 0m);
        }

        var filteredBins = companyBins;
        if (itemWarehouseId.HasValue && itemWarehouseId.Value != Guid.Empty)
        {
            filteredBins = filteredBins.Where(b => b.WarehouseId == itemWarehouseId.Value);
        }
        else if (transferFromWarehouseIds != null && transferFromWarehouseIds.Count > 0)
        {
            // Per ERPNext commit a07e6e3ce4: keep the item group's default warehouse (which receives the material)
            // even when it was selected among transferFromWarehouseIds.
            filteredBins = filteredBins.Where(b =>
                (itemGroupDefaultWarehouseId.HasValue && b.WarehouseId == itemGroupDefaultWarehouseId.Value)
                || !transferFromWarehouseIds.Contains(b.WarehouseId));
        }

        decimal actual = 0m, projected = 0m, reserved = 0m, ordered = 0m, planned = 0m;
        foreach (var bin in filteredBins)
        {
            actual += bin.ActualQty;
            projected += bin.ProjectedQty;
            reserved += bin.ReservedQtyForProduction;
            ordered += bin.OrderedQty;
            planned += bin.PlannedQty;
        }

        return (actual, projected, reserved, ordered, planned);
    }

    /// <summary>
    /// Computes the pending quantity of a Sales Order Item for a Production Plan, subtracting only non-Work-Order
    /// stock reservations from <paramref name="stockQty"/> and counting deliveries on top of reserved Work Order output
    /// per ERPNext commits 3464dc31dd, b3f47bf37d, ed2ce1f927, and PR #67479 (7549d6d5e8: <c>sales_order_planning.py</c>).
    /// </summary>
    public static decimal CalculateSalesOrderItemPendingQtyForProductionPlan(
        decimal stockQty,
        decimal stockReservedQty,
        decimal reservedOwnWorkOrderOutputQty,
        decimal workOrderQty,
        decimal deliveredQtyInOrderUom,
        decimal conversionFactor,
        decimal productionPlanQty,
        int precision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var reservedFromStock = stockReservedQty - reservedOwnWorkOrderOutputQty;
        var qty = stockQty - reservedFromStock;
        var deliveredOrReservedQty = (deliveredQtyInOrderUom * factor) + reservedOwnWorkOrderOutputQty;
        var pendingQty = qty - Math.Max(workOrderQty, Math.Max(deliveredOrReservedQty, 0m));

        // Per ERPNext PR #67479: cap by qty - productionPlanQty (not stockQty - productionPlanQty)
        // so the stock-reserved portion of the Sales Order is left out of Production Plan pending qty.
        return Math.Round(
            Math.Min(pendingQty, qty - productionPlanQty),
            precision,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes a Work Order's committed quantity against a Production Plan row, grossing up <paramref name="producedQty"/>
    /// by <c>1 - lossPercentage / 100</c> when <paramref name="creditProducedQty"/> is true so output above planned loss
    /// offsets another Work Order's excess process loss per ERPNext commit 273db39e00 (<c>work_order_quantities.py</c>),
    /// and capping closed Work Orders to their made qty per ERPNext PR #67481 (3515f99a79).
    /// </summary>
    public static decimal CalculateWorkOrderCommittedQtyForPlan(
        decimal workOrderQty,
        decimal producedQty,
        decimal processLossQty,
        decimal plannedLossPercentage,
        bool creditProducedQty = true,
        bool isClosed = false)
    {
        var lossFactor = 1m - (plannedLossPercentage / 100m);
        if (lossFactor <= 0m)
        {
            return Math.Max(0m, producedQty);
        }

        var goodQty = workOrderQty - Math.Max(processLossQty, workOrderQty * (plannedLossPercentage / 100m));
        var effectiveProduced = creditProducedQty ? (producedQty / lossFactor) : producedQty;
        var committedQty = Math.Max(0m, Math.Max(effectiveProduced, goodQty / lossFactor));

        if (isClosed)
        {
            // Per ERPNext PR #67481: a closed Work Order makes nothing more, so its unmade qty and loss
            // are freed to be ordered again; closing never raises its committed count.
            return Math.Min(committedQty, Math.Max(0m, producedQty) / lossFactor);
        }

        return committedQty;
    }

    /// <summary>
    /// Groups BOM subitems for Production Plan raw material calculation by <c>(ItemId, DoNotExplode)</c>,
    /// taking a <c>DoNotExplode</c> group's description and warehouse from its own lines, preventing <c>DoNotExplode</c>
    /// lines from using the item's <c>DefaultBomId</c>, and excluding exploded phantom lines from raw materials
    /// per ERPNext commits 0fab64a3df, 77f2a77489, 27e7009edd, and e813d87d5d (<c>bom_explosion.py</c>).
    /// </summary>
    public static List<(Guid ItemId, bool DoNotExplode, decimal Qty, string Description, Guid? SourceWarehouseId, Guid? ChildBomIdToExplode, bool ShouldMergeIntoRawMaterials)>
        EvaluateProductionPlanBomSubitemGroups(
            bool includeExplodedItems,
            bool includeSubcontractedItems,
            IEnumerable<(int Idx, Guid ItemId, bool DoNotExplode, bool IsPhantomItem, bool IsSubcontractedItem, decimal StockQtyPerBomUnit, string Description, Guid? SourceWarehouseId, Guid? LineBomId, Guid? ItemDefaultBomId)> bomLines,
            decimal plannedParentQty)
        => EvaluateProductionPlanBomSubitemGroups(
            includeExplodedItems,
            includeSubcontractedItems,
            bomLines,
            plannedParentQty,
            requestedSubAssemblies: null,
            plannedSubAssemblies: null);

    /// <summary>
    /// Classifies a Production Plan's sub-assembly rows per parent plan item into:
    /// - <c>RequestedSubAssemblies</c> (<c>MaterialRequest</c> / <c>Purchase</c>): requested directly via MR, never re-requested as raw material.
    /// - <c>PlannedSubAssemblies</c> (<c>InHouse</c> / <c>Subcontract</c>): made or subcontracted, so only their components are requested.
    /// Per ERPNext PR #67396 (commits 1449380676, c3437b2eba, 07c5dfcab8: <c>_get_sub_assemblies_by_plan_row</c>).
    /// </summary>
    public static (
        Dictionary<Guid, HashSet<(Guid ItemId, Guid? BomId)>> RequestedByPlanRow,
        Dictionary<Guid, Dictionary<(Guid ItemId, Guid? BomId), SubAssemblyType>> PlannedByPlanRow)
        ClassifySubAssembliesByPlanRow(
            IEnumerable<(Guid ProductionPlanItemId, Guid ProductionItemId, Guid? BomId, SubAssemblyType TypeOfManufacturing)>? subAssemblyRows)
    {
        var requested = new Dictionary<Guid, HashSet<(Guid ItemId, Guid? BomId)>>();
        var planned = new Dictionary<Guid, Dictionary<(Guid ItemId, Guid? BomId), SubAssemblyType>>();

        if (subAssemblyRows == null)
        {
            return (requested, planned);
        }

        foreach (var row in subAssemblyRows)
        {
            var key = (row.ProductionItemId, row.BomId);
            if (row.TypeOfManufacturing == SubAssemblyType.MaterialRequest)
            {
                if (!requested.TryGetValue(row.ProductionPlanItemId, out var set))
                {
                    set = new HashSet<(Guid, Guid?)>();
                    requested[row.ProductionPlanItemId] = set;
                }
                set.Add(key);
            }
            else
            {
                if (!planned.TryGetValue(row.ProductionPlanItemId, out var map))
                {
                    map = new Dictionary<(Guid, Guid?), SubAssemblyType>();
                    planned[row.ProductionPlanItemId] = map;
                }
                map[key] = row.TypeOfManufacturing;
            }
        }

        return (requested, planned);
    }

    /// <summary>
    /// Groups BOM subitems for Production Plan raw material calculation by <c>(ItemId, DoNotExplode)</c>,
    /// taking planned and requested sub-assemblies into account per ERPNext PR #67396:
    /// - A sub-assembly in <paramref name="requestedSubAssemblies"/> is skipped (already requested).
    /// - A planned sub-assembly is matched by <c>(ItemId, LineBomId)</c> (so a <c>DoNotExplode</c> line without a line BOM is still requested).
    /// - A planned <c>Subcontract</c> sub-assembly is skipped when <paramref name="includeSubcontractedItems"/> is false.
    /// - Otherwise, a planned sub-assembly (<c>InHouse</c> or <c>Subcontract</c>) explodes to request its components regardless of default request type.
    /// </summary>
    public static List<(Guid ItemId, bool DoNotExplode, decimal Qty, string Description, Guid? SourceWarehouseId, Guid? ChildBomIdToExplode, bool ShouldMergeIntoRawMaterials)>
        EvaluateProductionPlanBomSubitemGroups(
            bool includeExplodedItems,
            bool includeSubcontractedItems,
            IEnumerable<(int Idx, Guid ItemId, bool DoNotExplode, bool IsPhantomItem, bool IsSubcontractedItem, decimal StockQtyPerBomUnit, string Description, Guid? SourceWarehouseId, Guid? LineBomId, Guid? ItemDefaultBomId)> bomLines,
            decimal plannedParentQty,
            IReadOnlySet<(Guid ItemId, Guid? BomId)>? requestedSubAssemblies,
            IReadOnlyDictionary<(Guid ItemId, Guid? BomId), SubAssemblyType>? plannedSubAssemblies)
    {
        var results = new List<(Guid ItemId, bool DoNotExplode, decimal Qty, string Description, Guid? SourceWarehouseId, Guid? ChildBomIdToExplode, bool ShouldMergeIntoRawMaterials)>();
        if (bomLines == null)
        {
            return results;
        }

        var groups = bomLines
            .GroupBy(l => (l.ItemId, l.DoNotExplode))
            .OrderBy(g => g.Min(x => x.Idx));

        foreach (var group in groups)
        {
            var orderedLines = group.OrderBy(x => x.Idx).ToList();
            var representative = orderedLines[0];
            var totalQty = orderedLines.Sum(x => x.StockQtyPerBomUnit) * plannedParentQty;
            var allPhantom = orderedLines.All(x => x.IsPhantomItem);
            var lineBomId = orderedLines.Select(x => x.LineBomId).FirstOrDefault(id => id.HasValue && id.Value != Guid.Empty);
            var defaultBomId = orderedLines.Select(x => x.ItemDefaultBomId).FirstOrDefault(id => id.HasValue && id.Value != Guid.Empty);

            // A Do Not Explode line has no BOM, so it must not take the item's default BOM (0fab64a3df)
            var childBomId = lineBomId ?? (group.Key.DoNotExplode ? null : defaultBomId);

            if (requestedSubAssemblies != null && requestedSubAssemblies.Contains((group.Key.ItemId, childBomId)))
            {
                continue;
            }

            // Per PR #67396: match planned sub-assemblies by the BOM on the line (LineBomId)
            SubAssemblyType? plannedType = null;
            if (plannedSubAssemblies != null && plannedSubAssemblies.TryGetValue((group.Key.ItemId, lineBomId), out var matchedType))
            {
                plannedType = matchedType;
            }

            if (plannedType == SubAssemblyType.Subcontracting && !includeSubcontractedItems)
            {
                continue;
            }

            var explodable = allPhantom || (includeExplodedItems && childBomId.HasValue);
            var shouldExplodeSubitem = !group.Key.DoNotExplode
                && (!representative.IsSubcontractedItem || includeSubcontractedItems);
            var explode = explodable && (plannedType.HasValue || shouldExplodeSubitem) && totalQty > 0m && childBomId.HasValue;

            // An exploded line (including an exploded phantom line) is NOT merged into raw materials (27e7009edd)
            results.Add((
                group.Key.ItemId,
                group.Key.DoNotExplode,
                totalQty,
                representative.Description,
                representative.SourceWarehouseId,
                explode ? childBomId : null,
                !explode));
        }

        return results;
    }

    /// <summary>
    /// Allocates transfer quantities from available warehouse locations for a Production Plan Material Request item,
    /// skipping the item's own receiving warehouse so stock is never transferred from the receiving warehouse to itself
    /// while keeping subsequent transfer locations intact per ERPNext commits a5229ac21c and 3a06b94144 (<c>material_request.py</c>).
    /// </summary>
    public static (List<(Guid SourceWarehouseId, decimal TransferQty)> Transfers, decimal RemainingRequiredQty)
        AllocateTransferLocationsSkippingReceivingWarehouse(
            Guid? receivingWarehouseId,
            decimal requiredQty,
            IEnumerable<(Guid WarehouseId, decimal AvailableQty)>? availableLocations,
            int precision = 4)
    {
        var transfers = new List<(Guid SourceWarehouseId, decimal TransferQty)>();
        var remaining = requiredQty;

        if (availableLocations == null)
        {
            return (transfers, remaining);
        }

        foreach (var loc in availableLocations)
        {
            if (Math.Round(remaining, precision, MidpointRounding.AwayFromZero) <= 0m)
            {
                break;
            }

            if (receivingWarehouseId.HasValue && loc.WarehouseId == receivingWarehouseId.Value)
            {
                continue;
            }

            var qty = Math.Round(Math.Min(remaining, loc.AvailableQty), precision, MidpointRounding.AwayFromZero);
            if (qty <= 0m)
            {
                continue;
            }

            transfers.Add((loc.WarehouseId, qty));
            remaining -= qty;
        }

        return (transfers, remaining);
    }

    /// <summary>
    /// Combines fetched Sales Order items for a Production Plan while preserving Sales Order,
    /// Sales Order Item (<c>name</c>), and Product Bundle (<c>parent_item</c>) links
    /// per ERPNext PR #67477 (commit 4109c29d2d, <c>SalesOrderSourcingService</c>).
    /// </summary>
    public static List<CombinedSalesOrderPlanItem> CombineFetchedSalesOrderItems(
        IEnumerable<FetchedSalesOrderPlanRow> rows,
        bool combineItems,
        int precision = 4)
    {
        var result = new List<CombinedSalesOrderPlanItem>();
        if (rows == null)
        {
            return result;
        }

        if (!combineItems)
        {
            foreach (var r in rows)
            {
                result.Add(new CombinedSalesOrderPlanItem(
                    r.ItemId,
                    r.WarehouseId,
                    r.BomId,
                    Math.Round(r.PendingQty, precision, MidpointRounding.AwayFromZero),
                    r.SalesOrderId,
                    r.SalesOrderItemId,
                    r.ProductBundleItemId,
                    new List<(Guid SalesOrderId, Guid SalesOrderItemId, Guid? ProductBundleItemId, decimal Qty)>
                    {
                        (r.SalesOrderId, r.SalesOrderItemId, r.ProductBundleItemId, Math.Round(r.PendingQty, precision, MidpointRounding.AwayFromZero))
                    }));
            }
            return result;
        }

        foreach (var group in rows.GroupBy(r => (r.ItemId, r.BomId, r.WarehouseId)))
        {
            var members = group.ToList();
            var first = members[0];
            var totalQty = Math.Round(members.Sum(m => m.PendingQty), precision, MidpointRounding.AwayFromZero);
            var links = members
                .Select(m => (m.SalesOrderId, m.SalesOrderItemId, m.ProductBundleItemId, Math.Round(m.PendingQty, precision, MidpointRounding.AwayFromZero)))
                .ToList();

            result.Add(new CombinedSalesOrderPlanItem(
                first.ItemId,
                first.WarehouseId,
                first.BomId,
                totalQty,
                members.Count == 1 ? first.SalesOrderId : null,
                members.Count == 1 ? first.SalesOrderItemId : null,
                members.Count == 1 ? first.ProductBundleItemId : null,
                links));
        }

        return result;
    }

    /// <summary>
    /// Determines whether a Production Plan BOM sub-item should be exploded into its raw materials
    /// rather than requested directly: matches planned and requested sub-assemblies by <c>(ItemId, LineBomId)</c>,
    /// skips sub-assemblies already marked as <c>Material Request</c> or <c>Subcontract</c> when
    /// <paramref name="includeSubcontractedItems"/> is false, and explodes planned <c>In House</c> or
    /// <c>Subcontract</c> sub-assemblies regardless of item request type.
    /// Per ERPNext commits <c>1449380676</c>, <c>c3437b2eba</c>, and <c>07c5dfcab8</c> (<c>bom_explosion.py</c>).
    /// </summary>
    public static (bool SkipSubitem, bool ShouldExplode) EvaluatePlannedSubAssemblyExplosion(
        Guid itemId,
        Guid? lineBomId,
        decimal qty,
        bool isPhantomItem,
        bool includeExplodedItems,
        bool includeSubcontractedItems,
        bool defaultShouldExplode,
        IReadOnlySet<(Guid ItemId, Guid? BomId)>? requestedSubAssemblies,
        IReadOnlyDictionary<(Guid ItemId, Guid? BomId), string>? plannedSubAssemblyTypes)
    {
        var key = (itemId, lineBomId);
        if (requestedSubAssemblies != null && requestedSubAssemblies.Contains(key))
        {
            return (SkipSubitem: true, ShouldExplode: false);
        }

        string? plannedType = null;
        if (plannedSubAssemblyTypes != null)
        {
            plannedSubAssemblyTypes.TryGetValue(key, out plannedType);
        }

        if (string.Equals(plannedType, "Subcontract", StringComparison.OrdinalIgnoreCase) && !includeSubcontractedItems)
        {
            return (SkipSubitem: true, ShouldExplode: false);
        }

        var explodable = isPhantomItem || (includeExplodedItems && lineBomId.HasValue);
        var explode = explodable
            && (!string.IsNullOrEmpty(plannedType) || defaultShouldExplode)
            && qty > 0m;

        return (SkipSubitem: false, ShouldExplode: explode);
    }

    /// <summary>
    /// Resolves the set of Sales Order Item IDs that a Production Plan actually allocates planned qty to
    /// (combining direct plan rows and in-order allocated combined references rounded to <paramref name="precision"/>)
    /// so closed Sales Order rows receiving zero planned qty do not block plan submission.
    /// Per ERPNext commits <c>d6c1294e97</c> and <c>0b4b095b8d</c> (<c>validate_closed_sales_order_items</c>).
    /// </summary>
    public static HashSet<Guid> ResolvePlannedSalesOrderRowsForClosureCheck(
        IEnumerable<(Guid? DirectSalesOrderItemId, Guid PlanRowKey, decimal PlannedQty)>? planRows,
        IEnumerable<(Guid ItemReferenceKey, Guid SalesOrderItemId, decimal ReferenceQty)>? combinedReferences,
        int precision = 4)
    {
        var soItemIds = new HashSet<Guid>();
        var remainingByRefKey = new Dictionary<Guid, decimal>();

        if (planRows != null)
        {
            foreach (var row in planRows)
            {
                if (row.DirectSalesOrderItemId.HasValue && row.DirectSalesOrderItemId.Value != Guid.Empty)
                {
                    soItemIds.Add(row.DirectSalesOrderItemId.Value);
                }
                remainingByRefKey[row.PlanRowKey] = remainingByRefKey.GetValueOrDefault(row.PlanRowKey, 0m) + row.PlannedQty;
            }
        }

        if (combinedReferences != null)
        {
            foreach (var r in combinedReferences)
            {
                var avail = remainingByRefKey.GetValueOrDefault(r.ItemReferenceKey, 0m);
                var alloc = Math.Min(Math.Max(avail, 0m), Math.Max(r.ReferenceQty, 0m));
                remainingByRefKey[r.ItemReferenceKey] = avail - alloc;

                if (Math.Round(alloc, precision, MidpointRounding.AwayFromZero) > 0m)
                {
                    soItemIds.Add(r.SalesOrderItemId);
                }
            }
        }

        return soItemIds;
    }

    /// <summary>
    /// Computes the committed quantity counted for a Work Order against its Production Plan row
    /// per ERPNext PR #67481 (<c>production_plan/services/work_order_quantities.py: _get_committed_qty</c>):
    /// accounts for planned process loss, ensures excess loss does not erase already-produced goods,
    /// and when <paramref name="isClosed"/> is true caps the committed count at the Work Order's
    /// actually made quantity (<c>producedQty / (1 - lossPercentage / 100)</c>) so unmade qty is freed
    /// to be ordered again without ever raising the committed count.
    /// </summary>
    public static decimal CalculateProductionPlanCommittedWorkOrderQty(
        decimal workOrderQty,
        decimal producedQty,
        decimal processLossQty,
        decimal plannedLossPercentage,
        bool creditProducedQty = false,
        bool isClosed = false)
    {
        var lossPct = plannedLossPercentage > 0m && plannedLossPercentage < 100m ? plannedLossPercentage : 0m;
        var lossFactor = 1m - lossPct / 100m;

        var goodQty = workOrderQty - Math.Max(processLossQty, workOrderQty * lossPct / 100m);
        var effectiveProduced = creditProducedQty ? producedQty / lossFactor : producedQty;
        var committed = Math.Max(0m, Math.Max(effectiveProduced, goodQty / lossFactor));

        if (isClosed)
        {
            return Math.Min(committed, producedQty / lossFactor);
        }

        return committed;
    }

    /// <summary>
    /// Refuses to submit a Production Plan that references any closed Sales Order or closed
    /// Sales Order row (either directly on <c>po_items</c> or via positive allocated combined
    /// <c>prod_plan_references</c>) per ERPNext PR #68034
    /// (<c>ProductionPlan.validate_closed_sales_order_items</c>).
    /// </summary>
    public static void ValidateNoClosedSalesOrderRowsForPlanSubmit(
        IEnumerable<Guid> directSalesOrderItemIds,
        IEnumerable<(Guid SalesOrderItemId, decimal AllocatedQty)> combinedReferenceAllocations,
        IEnumerable<SalesOrderRowClosureInfo> salesOrderRows,
        int qtyPrecision = 4)
    {
        var referencedIds = new HashSet<Guid>(directSalesOrderItemIds.Where(id => id != Guid.Empty));
        foreach (var (soItemId, allocatedQty) in combinedReferenceAllocations)
        {
            if (soItemId != Guid.Empty && Math.Round(allocatedQty, qtyPrecision, MidpointRounding.AwayFromZero) > 0m)
            {
                referencedIds.Add(soItemId);
            }
        }

        if (referencedIds.Count == 0)
            return;

        var firstClosed = salesOrderRows
            .Where(r => referencedIds.Contains(r.SalesOrderItemId) && (r.IsRowClosed || r.IsOrderClosed))
            .OrderBy(r => r.SalesOrderNumber, StringComparer.Ordinal)
            .ThenBy(r => r.RowIdx)
            .FirstOrDefault();

        if (firstClosed != null)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("salesOrder", firstClosed.SalesOrderNumber)
                .WithData("rowIdx", firstClosed.RowIdx)
                .WithData("detail", $"Row #{firstClosed.RowIdx} ({firstClosed.ItemCode}) of Sales Order {firstClosed.SalesOrderNumber} is closed, so it cannot be planned.");
        }
    }

    /// <summary>
    /// Resolves the effective child BOM and explosion decision for a BOM sub-item row in Production Plan
    /// per ERPNext PR #67981 (<c>production_plan/services/bom_explosion.py: _process_subitem</c>):
    /// - A <paramref name="doNotExplode"/> line never inherits the item's <paramref name="defaultBomId"/>.
    /// - Explodes ONLY when (<paramref name="isPhantomItem"/> or (<paramref name="includeExplodedItems"/> and childBom != null)),
    ///   allowed by <paramref name="includeSubcontractedItems"/> (subcontracted items skip explosion when false), and <paramref name="qty"/> &gt; 0.
    /// - Otherwise requests the line as-is (<c>ShouldExplode = false</c>).
    /// </summary>
    public static (Guid? EffectiveChildBomId, bool ShouldExplode) ResolveBomExplosionSubitemChildBomAndMode(
        Guid? lineBomId,
        Guid? defaultBomId,
        bool doNotExplode,
        bool isPhantomItem,
        bool isSubcontractedItem,
        bool includeExplodedItems,
        bool includeSubcontractedItems,
        decimal qty)
    {
        var childBom = lineBomId.HasValue && lineBomId.Value != Guid.Empty
            ? lineBomId
            : (doNotExplode ? null : (defaultBomId.HasValue && defaultBomId.Value != Guid.Empty ? defaultBomId : null));

        var explodable = isPhantomItem || (includeExplodedItems && childBom.HasValue);
        var allowSubcontracted = !isSubcontractedItem || includeSubcontractedItems;
        var shouldExplode = explodable && allowSubcontracted && qty > 0m;

        return (childBom, shouldExplode);
    }
}

public sealed record SalesOrderRowClosureInfo(
    Guid SalesOrderItemId,
    string SalesOrderNumber,
    int RowIdx,
    string ItemCode,
    bool IsRowClosed,
    bool IsOrderClosed);

public sealed record FetchedSalesOrderPlanRow(
    Guid SalesOrderId,
    Guid SalesOrderItemId,
    Guid ItemId,
    Guid? WarehouseId,
    Guid? BomId,
    decimal PendingQty,
    Guid? ProductBundleItemId = null);

public sealed record CombinedSalesOrderPlanItem(
    Guid ItemId,
    Guid? WarehouseId,
    Guid? BomId,
    decimal PlannedQty,
    Guid? SalesOrderId,
    Guid? SalesOrderItemId,
    Guid? ProductBundleItemId,
    IReadOnlyList<(Guid SalesOrderId, Guid SalesOrderItemId, Guid? ProductBundleItemId, decimal Qty)> SalesOrderLinks);
