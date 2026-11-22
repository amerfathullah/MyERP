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
            purchaseInUom = Math.Ceiling(purchaseInUom);
        }

        return (transferStockQty, purchaseInUom);
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
                ? Math.Ceiling(purchaseQty)
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
    /// per ERPNext commits 3464dc31dd, b3f47bf37d, and ed2ce1f927 (<c>sales_order_planning.py</c>).
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

        return Math.Round(
            Math.Min(pendingQty, stockQty - productionPlanQty),
            precision,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes a Work Order's committed quantity against a Production Plan row, grossing up <paramref name="producedQty"/>
    /// by <c>1 - lossPercentage / 100</c> when <paramref name="creditProducedQty"/> is true so output above planned loss
    /// offsets another Work Order's excess process loss per ERPNext commit 273db39e00 (<c>work_order_quantities.py</c>).
    /// </summary>
    public static decimal CalculateWorkOrderCommittedQtyForPlan(
        decimal workOrderQty,
        decimal producedQty,
        decimal processLossQty,
        decimal plannedLossPercentage,
        bool creditProducedQty = true)
    {
        var lossFactor = 1m - (plannedLossPercentage / 100m);
        if (lossFactor <= 0m)
        {
            return Math.Max(0m, producedQty);
        }

        var goodQty = workOrderQty - Math.Max(processLossQty, workOrderQty * (plannedLossPercentage / 100m));
        var committedQty = goodQty / lossFactor;
        var effectiveProduced = creditProducedQty ? (producedQty / lossFactor) : producedQty;

        return Math.Max(0m, Math.Max(effectiveProduced, committedQty));
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
}

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
