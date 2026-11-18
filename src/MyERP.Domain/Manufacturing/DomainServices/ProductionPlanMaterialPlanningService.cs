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
}
