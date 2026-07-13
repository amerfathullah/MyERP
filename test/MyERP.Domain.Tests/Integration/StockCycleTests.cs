using System;
using System.Collections.Generic;
using MyERP.Inventory.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Integration tests validating stock and inventory flows.
/// Tests Bin projected qty, batch expiry, and tax calculation on purchase documents.
/// </summary>
public class StockCycleTests
{
    private readonly TaxesAndTotalsService _taxService = new();

    [Fact]
    public void Bin_ProjectedQty_CalculatesCorrectly()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Receive 100 units from PO
        bin.ApplyStockMovement(100, 5000); // 100 units at 50/unit
        bin.ActualQty.ShouldBe(100);
        bin.StockValue.ShouldBe(5000);
        bin.ValuationRate.ShouldBe(50);

        // Open orders: 50 more coming, 30 reserved for customers
        bin.OrderedQty = 50;
        bin.ReservedQty = 30;
        bin.PlannedQty = 20; // 20 planned from manufacturing

        // Projected = 100 + 50 + 20 - 30 = 140
        bin.ProjectedQty.ShouldBe(140);
    }

    [Fact]
    public void Bin_ApplyStockMovement_Updates_ValuationRate()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // First receipt: 10 at RM100 each
        bin.ApplyStockMovement(10, 1000);
        bin.ValuationRate.ShouldBe(100);

        // Second receipt: 10 at RM120 each (weighted average)
        bin.ApplyStockMovement(10, 1200);
        bin.ActualQty.ShouldBe(20);
        bin.StockValue.ShouldBe(2200);
        bin.ValuationRate.ShouldBe(110); // 2200/20

        // Issue 5 units
        bin.ApplyStockMovement(-5, -550); // 5 * 110
        bin.ActualQty.ShouldBe(15);
        bin.StockValue.ShouldBe(1650);
        bin.ValuationRate.ShouldBe(110);
    }

    [Fact]
    public void Bin_ProjectedQty_CanBeNegative()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.UpdateActualQty(10, 500);
        bin.ReservedQty = 15; // reserved more than available
        bin.ReservedQtyForProduction = 5;

        bin.ProjectedQty.ShouldBe(-10); // 10 - 15 - 5
    }

    [Fact]
    public void Batch_ExpiryTracking_EndToEnd()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "LOT-2026-001");
        batch.ManufacturingDate = new DateTime(2026, 1, 1);
        batch.ShelfLifeInDays = 180;

        batch.SetExpiryFromShelfLife();
        batch.ExpiryDate.ShouldBe(new DateTime(2026, 6, 30));

        // Check expiry as of different dates
        batch.IsExpired(new DateTime(2026, 6, 15)).ShouldBeFalse();
        batch.IsExpired(new DateTime(2026, 7, 1)).ShouldBeTrue();
    }

    [Fact]
    public void ItemPrice_ValidOnDate_WorksCorrectly()
    {
        var price = new ItemPrice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 99.90m, "Unit", "MYR")
        {
            ValidFrom = new DateTime(2026, 1, 1),
            ValidUpto = new DateTime(2026, 12, 31),
        };

        price.IsValidOnDate(new DateTime(2026, 6, 15)).ShouldBeTrue();
        price.IsValidOnDate(new DateTime(2025, 12, 31)).ShouldBeFalse();
        price.IsValidOnDate(new DateTime(2027, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void PurchaseInvoice_ValuationTax_ExcludedFromGrandTotal()
    {
        // Customs duty (Valuation only) + SST (Total) on a purchase
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 10, Rate = 500, NetAmount = 5000 },
        };
        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "PI", Guid.NewGuid(), 1, "Customs Duty 10%", "On Net Total", 10) { TaxCategory = "Valuation" },
            new(Guid.NewGuid(), "PI", Guid.NewGuid(), 2, "SST 6%", "On Net Total", 6) { TaxCategory = "Total" },
        };

        var totals = _taxService.Calculate(items, taxes);

        // Customs duty (500) adds to item cost but NOT to payable amount
        // Only SST (300) goes to grand total
        totals.TotalTax.ShouldBe(300m);
        totals.GrandTotal.ShouldBe(5300m);

        // But the Valuation row still has its calculated amount
        taxes[0].TaxAmount.ShouldBe(500m);
        taxes[1].TaxAmount.ShouldBe(300m);
    }

    [Fact]
    public void SerialNo_MaintenanceStatus_AutoUpdate()
    {
        var serial = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-2026-00001", Guid.NewGuid());
        serial.WarrantyExpiryDate = new DateTime(2027, 6, 30);
        serial.AmcExpiryDate = new DateTime(2028, 6, 30);

        // During warranty period
        serial.UpdateMaintenanceStatus(new DateTime(2026, 12, 1));
        serial.MaintenanceStatus.ShouldBe("Under Warranty");

        // After warranty, during AMC
        serial.UpdateMaintenanceStatus(new DateTime(2027, 12, 1));
        serial.MaintenanceStatus.ShouldBe("Under AMC");

        // After both expired
        serial.UpdateMaintenanceStatus(new DateTime(2029, 1, 1));
        serial.MaintenanceStatus.ShouldBe("Out of Warranty");
    }
}
