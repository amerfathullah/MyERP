using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Calculates gross profit metrics for sales documents.
/// Per ERPNext: valuation_rate = weighted average cost at time of billing/delivery.
/// Gross Profit = Selling Rate - Valuation Rate (per item × qty).
/// </summary>
public class GrossProfitService : DomainService
{
    private readonly StockValuationService _valuationService;

    public GrossProfitService(StockValuationService valuationService)
    {
        _valuationService = valuationService;
    }

    /// <summary>
    /// Populates ValuationRate on each invoice item from current stock valuation.
    /// Called during SI creation or submit to capture cost at point of sale.
    /// </summary>
    public async Task PopulateValuationRatesAsync(SalesInvoice invoice, Guid warehouseId)
    {
        foreach (var item in invoice.Items)
        {
            var balance = await _valuationService.GetCurrentBalanceAsync(
                item.ItemId, warehouseId);

            item.ValuationRate = balance.ValuationRate;
        }
    }

    /// <summary>
    /// Calculates aggregate gross profit metrics for an invoice.
    /// </summary>
    public GrossProfitResult CalculateForInvoice(SalesInvoice invoice)
    {
        var totalRevenue = invoice.Items.Sum(i => i.Quantity * i.UnitPrice);
        var totalCost = invoice.Items.Sum(i => i.Quantity * i.ValuationRate);
        var grossProfit = totalRevenue - totalCost;
        var grossProfitPercent = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100m : 0m;

        return new GrossProfitResult
        {
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            GrossProfit = grossProfit,
            GrossProfitPercentage = Math.Round(grossProfitPercent, 2),
            ItemDetails = invoice.Items.Select(i => new GrossProfitItemDetail
            {
                ItemId = i.ItemId,
                Description = i.Description,
                Quantity = i.Quantity,
                SellingRate = i.UnitPrice,
                ValuationRate = i.ValuationRate,
                GrossProfit = i.GrossProfit,
                GrossProfitPercentage = i.UnitPrice > 0
                    ? Math.Round(((i.UnitPrice - i.ValuationRate) / i.UnitPrice) * 100m, 2)
                    : 0m
            }).ToList()
        };
    }
}

public class GrossProfitResult
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitPercentage { get; set; }
    public List<GrossProfitItemDetail> ItemDetails { get; set; } = new();
}

public class GrossProfitItemDetail
{
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal SellingRate { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitPercentage { get; set; }
}
