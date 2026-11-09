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
        var duplicateMap = new Dictionary<(string ItemCode, string Warehouse), (DownloadRawMaterialRow Row, decimal ConversionFactor, decimal Quantity)>();

        foreach (var d in items)
        {
            var key = (d.ItemCode.Trim(), d.Warehouse.Trim());
            var convFactor = d.ConversionFactor > 0m ? d.ConversionFactor : 1m;
            var stockQty = d.Quantity * convFactor;

            if (duplicateMap.TryGetValue(key, out var merged))
            {
                // Transfer rows in stock UOM, purchase rows in purchase UOM -> merge in first row's UOM
                merged.Quantity += stockQty / merged.ConversionFactor;
                merged.Row.RequiredQty = Math.Round(merged.Quantity, precision);
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
