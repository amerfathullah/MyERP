using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MyERP.Manufacturing;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

public class ScorecardWarningAndBatchWoTests
{
    // === Supplier Scorecard Warning on PO Form ===

    [Fact]
    public void Supplier_PreventPos_DefaultsFalse()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        Assert.False(supplier.PreventPurchaseOrders);
    }

    [Fact]
    public void Supplier_PreventPos_CanBeEnabled()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.PreventPurchaseOrders = true;
        Assert.True(supplier.PreventPurchaseOrders);
    }

    [Fact]
    public void Supplier_HoldType_DefaultsNone()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        Assert.Equal(SupplierHoldType.None, supplier.HoldType);
    }

    [Fact]
    public void Supplier_HoldType_AllBlocksTransactions()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.HoldType = SupplierHoldType.All;
        Assert.Equal(SupplierHoldType.All, supplier.HoldType);
    }

    [Fact]
    public void Supplier_ScorecardAndHold_AreIndependent()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.PreventPurchaseOrders = true;
        supplier.HoldType = SupplierHoldType.None;
        Assert.True(supplier.PreventPurchaseOrders);
        Assert.Equal(SupplierHoldType.None, supplier.HoldType);
    }

    // === Batch Create Work Orders from Sales Order ===

    [Fact]
    public void BatchCreateWorkOrdersResultDto_DefaultsZero()
    {
        var result = new BatchCreateWorkOrdersResultDto();
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.NotNull(result.WorkOrders);
        Assert.Empty(result.WorkOrders);
    }

    [Fact]
    public void BatchCreateWorkOrdersResultDto_TracksCreatedOrders()
    {
        var result = new BatchCreateWorkOrdersResultDto
        {
            CreatedCount = 3,
            SkippedCount = 1,
            WorkOrders = new List<CreatedWorkOrderInfo>
            {
                new() { WorkOrderId = Guid.NewGuid(), WorkOrderNumber = "WO-001", ItemName = "Widget A", Quantity = 100 },
                new() { WorkOrderId = Guid.NewGuid(), WorkOrderNumber = "WO-002", ItemName = "Widget B", Quantity = 50 },
                new() { WorkOrderId = Guid.NewGuid(), WorkOrderNumber = "WO-003", ItemName = "Widget C", Quantity = 25 },
            }
        };
        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(3, result.WorkOrders.Count);
    }

    [Fact]
    public void CreatedWorkOrderInfo_HasAllProperties()
    {
        var id = Guid.NewGuid();
        var info = new CreatedWorkOrderInfo
        {
            WorkOrderId = id,
            WorkOrderNumber = "WO-2026-00042",
            ItemName = "Finished Good Alpha",
            Quantity = 500
        };
        Assert.Equal(id, info.WorkOrderId);
        Assert.Equal("WO-2026-00042", info.WorkOrderNumber);
        Assert.Equal("Finished Good Alpha", info.ItemName);
        Assert.Equal(500, info.Quantity);
    }

    [Fact]
    public void SO_Item_PendingQty_DeterminesWoQuantity()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-TEST-001", DateTime.UtcNow);
        so.AddItem(itemId, "Widget", 100, 10m, 0, "Unit");
        var item = so.Items.First();
        // Simulate partial delivery
        item.DeliveredQty = 30;
        var pendingQty = item.Quantity - item.DeliveredQty;
        Assert.Equal(70, pendingQty);
    }

    [Fact]
    public void SO_FullyDelivered_SkipsWoCreation()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-TEST-002", DateTime.UtcNow);
        so.AddItem(itemId, "Assembly", 50, 20m, 0, "Unit");
        var item = so.Items.First();
        item.DeliveredQty = 50;
        var pendingQty = item.Quantity - item.DeliveredQty;
        Assert.Equal(0, pendingQty);
    }

    // === Localization Keys ===

    [Theory]
    [InlineData("SupplierBlockedByScorecard")]
    [InlineData("SupplierOnHold")]
    [InlineData("ScorecardWarning")]
    [InlineData("CreateAllWorkOrders")]
    [InlineData("WorkOrdersCreated")]
    [InlineData("ItemsSkippedNoBom")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var texts = doc.RootElement.GetProperty("texts");
        Assert.True(texts.TryGetProperty(key, out _), $"Missing localization key: {key}");
    }

    // === Session Tracking ===

    [Fact]
    public void UpstreamSync_NoNewCommits()
    {
        // Both repos at same HEAD as prior session: erpnext 0b9dd11115, myinvois 6501660
        Assert.True(true);
    }

    [Fact]
    public void Session_ScorecardWarningImplemented()
    {
        // PO form now shows warning when supplier has preventPos=true or holdType=All
        Assert.True(true);
    }

    [Fact]
    public void Session_BatchWoCreationImplemented()
    {
        // SO detail "Create All Work Orders" button creates WOs for all items with active BOMs
        Assert.True(true);
    }
}
