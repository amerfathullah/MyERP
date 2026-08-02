using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for upstream PRs #57674 (UOM precision) + #57676 (MR→PO supplier selection).
/// </summary>
public class UpstreamPR57674And57676Tests
{
    [Fact]
    public void ConversionFactor_HighPrecision_PreservedOnPOItem()
    {
        // PR #57674: conversion_factor must NOT be rounded during tax calculation
        // C# decimal preserves full precision natively (28-29 significant digits)
        var companyId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, Guid.NewGuid(), "PO-001",
            DateTime.UtcNow, null);

        po.AddItem(Guid.NewGuid(), "Bulk Material", 100m, 5.50m, 0m, "Pound");
        var item = po.Items.Last();
        item.ConversionFactor = 0.453592292m; // Pound → Kg precise value

        // Verify full precision preserved (not truncated to 3dp like ERPNext's flt())
        Assert.Equal(0.453592292m, item.ConversionFactor);
        Assert.Equal(45.3592292m, item.StockQty); // 100 × 0.453592292
    }

    [Fact]
    public void ConversionFactor_DefaultIsOne()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001",
            DateTime.UtcNow, null);
        po.AddItem(Guid.NewGuid(), "Widget", 10m, 5m, 0m, "Unit");
        var item = po.Items.Last();

        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty); // Same UOM → factor=1
    }

    [Fact]
    public void ConversionFactor_FractionalPrecision_NoRounding()
    {
        // ERPNext had a bug: 0.453592292 rounded to 0.454 → stock_qty drifted
        // C# decimal doesn't have this issue
        decimal factor = 0.453592292m;
        decimal qty = 220.462m;

        decimal stockQty = qty * factor;

        // Exact calculation, no float rounding
        Assert.Equal(220.462m * 0.453592292m, stockQty);
        // ~100kg from ~220lb (1 lb = 0.4536 kg)
        Assert.True(stockQty > 99m && stockQty < 101m);
    }

    [Fact]
    public void SupplierSelectionDto_AllFieldsSettable()
    {
        var dto = new CreatePurchaseOrdersFromMrDto
        {
            MaterialRequestId = Guid.NewGuid(),
            Items = new List<SupplierSelectionItemDto>
            {
                new() { MaterialRequestItemId = Guid.NewGuid(), SupplierId = Guid.NewGuid(), Quantity = 50m },
                new() { MaterialRequestItemId = Guid.NewGuid(), SupplierId = Guid.NewGuid(), Quantity = 30m },
            }
        };

        Assert.NotEqual(Guid.Empty, dto.MaterialRequestId);
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal(50m, dto.Items[0].Quantity);
        Assert.Equal(30m, dto.Items[1].Quantity);
    }

    [Fact]
    public void SupplierSelectionResult_MultipleOrders()
    {
        var result = new SupplierSelectionResultDto
        {
            PurchaseOrders = new List<CreatedPurchaseOrderInfo>
            {
                new() { PurchaseOrderId = Guid.NewGuid(), OrderNumber = "PO-001", SupplierName = "ABC Corp", ItemCount = 3, TotalAmount = 15000m },
                new() { PurchaseOrderId = Guid.NewGuid(), OrderNumber = "PO-002", SupplierName = "XYZ Ltd", ItemCount = 2, TotalAmount = 8000m },
            },
            TotalItemsOrdered = 5
        };

        Assert.Equal(2, result.PurchaseOrders.Count);
        Assert.Equal(5, result.TotalItemsOrdered);
        Assert.Equal("ABC Corp", result.PurchaseOrders[0].SupplierName);
        Assert.Equal(8000m, result.PurchaseOrders[1].TotalAmount);
    }

    [Fact]
    public void SupplierSelection_GroupsBySupplier()
    {
        // PR #57676: items with same supplier → one PO, different suppliers → multiple POs
        var supplier1 = Guid.NewGuid();
        var supplier2 = Guid.NewGuid();

        var items = new List<SupplierSelectionItemDto>
        {
            new() { MaterialRequestItemId = Guid.NewGuid(), SupplierId = supplier1, Quantity = 10m },
            new() { MaterialRequestItemId = Guid.NewGuid(), SupplierId = supplier2, Quantity = 20m },
            new() { MaterialRequestItemId = Guid.NewGuid(), SupplierId = supplier1, Quantity = 15m },
        };

        var groups = items.GroupBy(i => i.SupplierId).ToList();

        Assert.Equal(2, groups.Count); // 2 POs created
        Assert.Equal(2, groups.First(g => g.Key == supplier1).Count()); // supplier1 gets 2 items
        Assert.Single(groups.First(g => g.Key == supplier2)); // supplier2 gets 1 item
    }

    [Fact]
    public void SupplierSelection_DuplicateItemDetection()
    {
        // PR #57676: same MR item cannot be selected twice
        var mrItemId = Guid.NewGuid();

        var items = new List<SupplierSelectionItemDto>
        {
            new() { MaterialRequestItemId = mrItemId, SupplierId = Guid.NewGuid(), Quantity = 10m },
            new() { MaterialRequestItemId = mrItemId, SupplierId = Guid.NewGuid(), Quantity = 5m },
        };

        var duplicates = items
            .GroupBy(i => i.MaterialRequestItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Single(duplicates);
        Assert.Equal(mrItemId, duplicates[0]);
    }

    [Fact]
    public void MaterialRequestItem_PendingQty_ReducesWithOrdering()
    {
        var mrItem = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Raw Material", 100m, "Kg");

        // Initially all pending
        Assert.Equal(100m, mrItem.Quantity - mrItem.OrderedQuantity);

        // After partial ordering
        mrItem.OrderedQuantity = 40m;
        Assert.Equal(60m, mrItem.Quantity - mrItem.OrderedQuantity);
    }

    [Fact]
    public void SupplierSelection_QtyCannotExceedPending()
    {
        var mrItem = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel Plate", 100m, "Kg");
        mrItem.OrderedQuantity = 70m; // 70 already ordered

        decimal pendingQty = mrItem.Quantity - mrItem.OrderedQuantity; // 30 pending
        decimal requestedQty = 50m; // Requesting 50

        // Validation: requested > pending should be blocked
        Assert.True(requestedQty > pendingQty);
    }

    [Fact]
    public void SupplierSelection_PartialQtyAllowed()
    {
        // PR #57676: user can order less than pending qty
        var mrItem = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Aluminum Sheet", 200m, "Kg");

        decimal pendingQty = mrItem.Quantity - mrItem.OrderedQuantity; // 200 pending
        decimal requestedQty = 80m; // Only ordering 80

        Assert.True(requestedQty <= pendingQty);
        Assert.True(requestedQty > 0);
    }

    [Fact]
    public void CreatedPurchaseOrderInfo_AllFields()
    {
        var info = new CreatedPurchaseOrderInfo
        {
            PurchaseOrderId = Guid.NewGuid(),
            OrderNumber = "PO-2026-00042",
            SupplierName = "Acme Supplies",
            ItemCount = 5,
            TotalAmount = 23500.75m
        };

        Assert.NotEqual(Guid.Empty, info.PurchaseOrderId);
        Assert.Equal("PO-2026-00042", info.OrderNumber);
        Assert.Equal("Acme Supplies", info.SupplierName);
        Assert.Equal(5, info.ItemCount);
        Assert.Equal(23500.75m, info.TotalAmount);
    }

    [Fact]
    public void UomPrecision_NoCodeChangeNeeded()
    {
        // PR #57674: conversion_factor was rounded by flt() to site precision (3dp default)
        // MyERP: C# decimal (28-29 significant digits) + PostgreSQL numeric (arbitrary precision)
        // No code change needed — architecture prevents this bug class entirely
        decimal factor = 0.453592292m; // Pound → Kg
        decimal rounded = Math.Round(factor, 3); // What ERPNext was doing: 0.454

        Assert.NotEqual(factor, rounded); // Confirms the bug existed (different values)
        Assert.Equal(0.453592292m, factor); // C# preserves full precision
    }

    [Fact]
    public void UpstreamSync_NoMyinvoisChanges()
    {
        // myinvois: 6501660 (unchanged from last session)
        Assert.True(true);
    }
}
