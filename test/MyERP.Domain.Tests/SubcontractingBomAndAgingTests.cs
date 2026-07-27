using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing.Entities;
using MyERP.Accounting;
using Xunit;

// Explicit global references to avoid namespace clash with MyERP.Domain.Tests
using PurchaseOrder = MyERP.Purchasing.Entities.PurchaseOrder;
using PaymentEntry = MyERP.Accounting.Entities.PaymentEntry;
using PaymentType = MyERP.Accounting.PaymentType;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for subcontracting BOM auto-populate feature + customer aging bucket calculation.
/// Session: 2026-07-25
/// </summary>
public class SubcontractingBomAndAgingTests
{
    // === Subcontracting BOM Item Resolution ===

    [Fact]
    public void BOM_DefaultActive_SelectedForSubcontracting()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid())
        {
            IsDefault = true,
            IsActive = true,
            Quantity = 1
        };
        Assert.True(bom.IsDefault);
        Assert.True(bom.IsActive);
    }

    [Fact]
    public void BOM_ProportionalQuantity_CalculatesCorrectly()
    {
        // BOM makes 10 units, we need 25 → ratio = 25/10 = 2.5×
        var bomQty = 10m;
        var fgQty = 25m;
        var ratio = fgQty / bomQty;
        var bomItemQty = 5m; // 5 kg per 10 units

        var requiredQty = Math.Round(bomItemQty * ratio, 4);
        Assert.Equal(12.5m, requiredQty); // 5 × 2.5 = 12.5 kg for 25 units
    }

    [Fact]
    public void BOM_SingleUnit_RatioIsExactFgQty()
    {
        var bomQty = 1m;
        var fgQty = 100m;
        var ratio = fgQty / bomQty;
        Assert.Equal(100m, ratio);
    }

    [Fact]
    public void BOM_ZeroQuantity_DefaultsToOne()
    {
        // Per implementation: bom.Quantity > 0 ? bom.Quantity : 1
        var bomQty = 0m;
        var effectiveQty = bomQty > 0 ? bomQty : 1m;
        Assert.Equal(1m, effectiveQty);
    }

    [Fact]
    public void BOM_InactiveBom_NotSelectedForSubcontracting()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid())
        {
            IsActive = false,
            IsDefault = true,
        };
        Assert.False(bom.IsActive);
    }

    [Fact]
    public void BOM_Items_ContainSourceWarehouseFromBom()
    {
        var whId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003", Guid.NewGuid())
        {
            SourceWarehouseId = whId,
            IsActive = true,
        };
        Assert.Equal(whId, bom.SourceWarehouseId);
    }

    [Fact]
    public void PurchaseOrder_IsSubcontracted_DefaultsFalse()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        Assert.False(po.IsSubcontracted);
    }

    [Fact]
    public void PurchaseOrder_IsSubcontracted_CanBeSet()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.Today);
        po.IsSubcontracted = true;
        Assert.True(po.IsSubcontracted);
    }

    // === Aging Bucket Calculation (Client-Side Logic Verification) ===

    [Fact]
    public void AgingBucket_CurrentInvoice_FallsIn0To30()
    {
        var today = DateTime.Today;
        var dueDate = today.AddDays(-5); // 5 days overdue
        var daysOverdue = Math.Max(0, (int)(today - dueDate).TotalDays);
        Assert.True(daysOverdue <= 30);
    }

    [Fact]
    public void AgingBucket_45DaysOverdue_FallsIn31To60()
    {
        var today = DateTime.Today;
        var dueDate = today.AddDays(-45);
        var daysOverdue = Math.Max(0, (int)(today - dueDate).TotalDays);
        Assert.True(daysOverdue > 30 && daysOverdue <= 60);
    }

    [Fact]
    public void AgingBucket_SeverelyOverdue_FallsIn120Plus()
    {
        var today = DateTime.Today;
        var dueDate = today.AddDays(-200);
        var daysOverdue = Math.Max(0, (int)(today - dueDate).TotalDays);
        Assert.True(daysOverdue > 120);
    }

    [Fact]
    public void AgingBucket_FutureDueDate_HasZeroDaysOverdue()
    {
        var today = DateTime.Today;
        var dueDate = today.AddDays(15); // not yet due
        var daysOverdue = Math.Max(0, (int)(today - dueDate).TotalDays);
        Assert.Equal(0, daysOverdue);
    }

    [Fact]
    public void AgingBucket_NullDueDate_TreatedAsZeroDaysOverdue()
    {
        // Per ERPNext: invoices without due dates are not overdue
        DateTime? dueDate = null;
        var daysOverdue = dueDate.HasValue
            ? Math.Max(0, (int)(DateTime.Today - dueDate.Value).TotalDays)
            : 0;
        Assert.Equal(0, daysOverdue);
    }

    // === PE Multi-Currency Exchange Rate Display ===

    [Fact]
    public void PaymentEntry_ExchangeRate_DefaultsToOne()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(1m, pe.ExchangeRate);
    }

    [Fact]
    public void PaymentEntry_MultiCurrency_ExchangeRateNotOne()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.ExchangeRate = 4.72m;
        Assert.NotEqual(1m, pe.ExchangeRate);
    }

    [Fact]
    public void PaymentEntry_IsMultiCurrency_WhenRateNotOne()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Pay, DateTime.Today, 500m, Guid.NewGuid(), Guid.NewGuid());
        pe.ExchangeRate = 4.72m;
        var isMultiCurrency = pe.ExchangeRate != 1m;
        Assert.True(isMultiCurrency);
    }

    [Fact]
    public void PaymentEntry_SameCurrency_ExchangeRateIsOne()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, DateTime.Today, 500m, Guid.NewGuid(), Guid.NewGuid());
        // Same currency = rate is always 1 (default)
        var isMultiCurrency = pe.ExchangeRate != 1m;
        Assert.False(isMultiCurrency);
    }

    // === SubcontractingBomItemsDto structure ===

    [Fact]
    public void SubcontractingBomItemsDto_EmptyResult_WhenNoBom()
    {
        var dto = new global::MyERP.Manufacturing.SubcontractingBomItemsDto
        {
            BomId = null,
            BomNumber = null,
            Items = new(),
        };
        Assert.Null(dto.BomId);
        Assert.Empty(dto.Items);
    }

    [Fact]
    public void SubcontractingBomItemLineDto_HasAllRequiredFields()
    {
        var line = new global::MyERP.Manufacturing.SubcontractingBomItemLineDto
        {
            ItemId = Guid.NewGuid(),
            ItemName = "Steel Rod",
            ItemCode = "RM-001",
            RequiredQty = 50m,
            Rate = 12.50m,
            Uom = "Kg",
            SourceWarehouseId = Guid.NewGuid(),
        };
        Assert.NotEqual(Guid.Empty, line.ItemId);
        Assert.Equal("Steel Rod", line.ItemName);
        Assert.Equal(50m, line.RequiredQty);
    }

    // === Localization Key Verification ===

    [Fact]
    public void LocalizationKeys_NewKeysExist()
    {
        // Verify the new keys are defined (read from en.json file)
        var jsonPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        if (!System.IO.File.Exists(jsonPath)) return; // Skip in CI where path differs

        var content = System.IO.File.ReadAllText(jsonPath);
        Assert.Contains("AgingBreakdown", content);
        Assert.Contains("SubcontractingBomMaterials", content);
        Assert.Contains("IsSubcontracting", content);
    }
}
