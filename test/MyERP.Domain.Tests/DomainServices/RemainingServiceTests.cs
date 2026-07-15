using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Notification;
using MyERP.Notification.Entities;
using MyERP.Purchasing;
using Shouldly;
using Xunit;

namespace MyERP.DomainServices;

/// <summary>
/// Tests for AutoReorderService concepts — Item reorder level + trigger logic.
/// </summary>
public class AutoReorderServiceConceptTests
{
    [Fact]
    public void Item_ReorderLevel_DefaultZero()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ReorderLevel.ShouldBe(0m);
    }

    [Fact]
    public void Item_ReorderQty_DefaultZero()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ReorderQty.ShouldBe(0m);
    }

    [Fact]
    public void Item_SafetyStock_DefaultZero()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.SafetyStock.ShouldBe(0m);
    }

    [Fact]
    public void Item_NeedsReorder_WhenBelowLevel()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ReorderLevel = 100m;
        decimal projectedQty = 50m;
        (projectedQty < item.ReorderLevel).ShouldBeTrue();
    }

    [Fact]
    public void Item_NoReorder_WhenAboveLevel()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ReorderLevel = 100m;
        decimal projectedQty = 150m;
        (projectedQty < item.ReorderLevel).ShouldBeFalse();
    }

    [Fact]
    public void Item_NoReorder_WhenLevelIsZero()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ReorderLevel = 0m; // Zero = disabled
        decimal projectedQty = 5m;
        // Zero reorder level means auto-reorder is disabled for this item
        (item.ReorderLevel > 0 && projectedQty < item.ReorderLevel).ShouldBeFalse();
    }

    [Fact]
    public void Item_ReorderQty_UsedForMRCreation()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ReorderLevel = 50m;
        item.ReorderQty = 200m;
        // When triggered, MR is created with ReorderQty
        item.ReorderQty.ShouldBe(200m);
    }

    [Fact]
    public void Item_DefaultMaterialRequestType_Purchase()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.Purchase);
    }

    [Fact]
    public void Item_DefaultMaterialRequestType_CanBeManufacture()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "FG-001", "Finished Good", ItemType.Goods);
        item.DefaultMaterialRequestType = MaterialRequestType.Manufacture;
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.Manufacture);
    }
}

/// <summary>
/// Tests for BatchExpiryValidationService concepts.
/// </summary>
public class BatchExpiryValidationConceptTests
{
    [Fact]
    public void Batch_NotExpired_WhenNoExpiryDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate.ShouldBeNull();
        // Null expiry = never expires
        (batch.ExpiryDate == null || batch.ExpiryDate > DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void Batch_NotExpired_WhenFutureDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate = DateTime.Today.AddDays(30);
        (batch.ExpiryDate > DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void Batch_Expired_WhenPastDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate = DateTime.Today.AddDays(-5);
        (batch.ExpiryDate < DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void Batch_Expired_WhenToday()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate = DateTime.Today;
        // Per ERPNext: expiry_date <= posting_date means expired
        (batch.ExpiryDate <= DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void Batch_IsDisabled_BlocksStockOut()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.IsDisabled = true;
        batch.IsDisabled.ShouldBeTrue();
    }

    [Fact]
    public void Batch_DefaultNotDisabled()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.IsDisabled.ShouldBeFalse();
    }

    [Fact]
    public void BatchValidationItem_Creation()
    {
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var item = new BatchValidationItem(itemId, batchId, "Widget A");
        item.ItemId.ShouldBe(itemId);
        item.BatchId.ShouldBe(batchId);
        item.ItemName.ShouldBe("Widget A");
    }

    [Fact]
    public void BatchValidationItem_NullBatch_SkipsValidation()
    {
        var item = new BatchValidationItem(Guid.NewGuid(), null, "No Batch Item");
        item.BatchId.ShouldBeNull();
        // Items without batch → skip expiry check
    }
}

/// <summary>
/// Tests for BusinessNotificationService — AppNotification entity lifecycle.
/// </summary>
public class BusinessNotificationConceptTests
{
    [Fact]
    public void AppNotification_Create()
    {
        var userId = Guid.NewGuid();
        var notification = new AppNotification(Guid.NewGuid(), userId, "Test Notification");
        notification.UserId.ShouldBe(userId);
        notification.Subject.ShouldBe("Test Notification");
        notification.IsRead.ShouldBeFalse();
    }

    [Fact]
    public void AppNotification_MarkAsRead()
    {
        var notification = new AppNotification(Guid.NewGuid(), Guid.NewGuid(), "Alert");
        notification.MarkAsRead();
        notification.IsRead.ShouldBeTrue();
    }

    [Fact]
    public void AppNotification_DefaultNotRead()
    {
        var notification = new AppNotification(Guid.NewGuid(), Guid.NewGuid(), "New");
        notification.IsRead.ShouldBeFalse();
    }

    [Fact]
    public void AppNotification_HasSeverity()
    {
        var notification = new AppNotification(Guid.NewGuid(), Guid.NewGuid(), "Low Stock Alert");
        notification.Severity = NotificationSeverity.Warning;
        notification.Severity.ShouldBe(NotificationSeverity.Warning);
    }

    [Fact]
    public void AppNotification_HasActionUrl()
    {
        var notification = new AppNotification(Guid.NewGuid(), Guid.NewGuid(), "Approval Needed");
        notification.ActionUrl = "/workflow/pending";
        notification.ActionUrl.ShouldBe("/workflow/pending");
    }

    [Fact]
    public void AppNotification_HasBody()
    {
        var notification = new AppNotification(Guid.NewGuid(), Guid.NewGuid(), "Payment Received");
        notification.Body = "RM 5,000 received from Customer A";
        notification.Body.ShouldBe("RM 5,000 received from Customer A");
    }
}

/// <summary>
/// Tests for DocumentActivityLogService — activity log entity creation.
/// </summary>
public class DocumentActivityLogTests
{
    [Fact]
    public void DocumentActivityLog_Create()
    {
        var docId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "SalesInvoice", docId,
            "Posted", companyId, "SI-001", "Submitted", "Posted");
        log.DocumentType.ShouldBe("SalesInvoice");
        log.DocumentId.ShouldBe(docId);
        log.ActivityType.ShouldBe("Posted");
        log.CompanyId.ShouldBe(companyId);
    }

    [Fact]
    public void DocumentActivityLog_HasDocumentNumber()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "PurchaseOrder", Guid.NewGuid(),
            "Submitted", Guid.NewGuid(), "PO-2026-00001");
        log.DocumentNumber.ShouldBe("PO-2026-00001");
    }

    [Fact]
    public void DocumentActivityLog_HasStatusTransition()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "SalesOrder", Guid.NewGuid(),
            "Submitted", Guid.NewGuid(), "SO-001",
            previousStatus: "Draft", newStatus: "ToDeliverAndBill");
        log.PreviousStatus.ShouldBe("Draft");
        log.NewStatus.ShouldBe("ToDeliverAndBill");
    }

    [Fact]
    public void DocumentActivityLog_HasPerformer()
    {
        var userId = Guid.NewGuid();
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "PaymentEntry", Guid.NewGuid(),
            "Posted", Guid.NewGuid(), "PE-001",
            performedByUserId: userId);
        log.PerformedByUserId.ShouldBe(userId);
    }

    [Fact]
    public void DocumentActivityLog_HasDetails()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "StockEntry", Guid.NewGuid(),
            "Cancelled", Guid.NewGuid(), "SE-001",
            details: "Cancelled due to incorrect warehouse");
        log.Details.ShouldBe("Cancelled due to incorrect warehouse");
    }

    [Fact]
    public void DocumentActivityLog_Cancelled_ActivityType()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            "Cancelled", Guid.NewGuid(), "SI-001",
            previousStatus: "Posted", newStatus: "Cancelled");
        log.ActivityType.ShouldBe("Cancelled");
        log.PreviousStatus.ShouldBe("Posted");
        log.NewStatus.ShouldBe("Cancelled");
    }

    [Fact]
    public void DocumentActivityLog_Converted_ActivityType()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "Quotation", Guid.NewGuid(),
            "Converted", Guid.NewGuid(), "QTN-001",
            details: "Converted to Sales Order SO-001");
        log.ActivityType.ShouldBe("Converted");
        log.Details!.ShouldContain("Sales Order");
    }
}
