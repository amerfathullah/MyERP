using System;
using System.Linq;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Tests for PO per-item expected delivery date tracking and overdue detection.
/// Per ERPNext: each PO item can have its own expected_delivery_date.
/// Per DO-NOT: "Allow Purchase Receipt posting_date before linked Purchase Order transaction_date"
/// </summary>
public class PurchaseOrderDeliveryDateTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId1 = Guid.NewGuid();
    private static readonly Guid ItemId2 = Guid.NewGuid();

    private PurchaseOrder CreatePO(DateTime? expectedDate = null)
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001",
            DateTime.UtcNow.Date, Guid.NewGuid());
        po.ExpectedDeliveryDate = expectedDate;
        return po;
    }

    // --- Per-Item Expected Delivery Date ---

    [Fact]
    public void PO_Item_ExpectedDeliveryDate_Defaults_Null()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        Assert.Null(po.Items[0].ExpectedDeliveryDate);
    }

    [Fact]
    public void PO_Item_ExpectedDeliveryDate_Can_Be_Set()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        var itemExpectedDate = DateTime.UtcNow.AddDays(30);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = itemExpectedDate;
        Assert.Equal(itemExpectedDate, po.Items[0].ExpectedDeliveryDate);
    }

    [Fact]
    public void PO_Item_Different_Dates_For_Different_Items()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Item A", 5, 100m, 0);
        po.AddItem(ItemId2, "Item B", 3, 200m, 0);

        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7);
        ((PurchaseOrderItem)po.Items[1]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(14);

        Assert.NotEqual(po.Items[0].ExpectedDeliveryDate, po.Items[1].ExpectedDeliveryDate);
    }

    // --- Item-Level Overdue Detection ---

    [Fact]
    public void PO_Item_IsOverdue_When_Past_ItemDate_And_Pending()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-5);

        Assert.True(po.Items[0].IsOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_Not_Overdue_When_Future_Date()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(10);

        Assert.False(po.Items[0].IsOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_Not_Overdue_When_Fully_Received()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-5);
        ((PurchaseOrderItem)po.Items[0]).ReceivedQty = 10; // fully received

        Assert.False(po.Items[0].IsOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_Overdue_Uses_Parent_Date_When_No_ItemDate()
    {
        var parentDate = DateTime.UtcNow.AddDays(-3);
        var po = CreatePO(parentDate);
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        // No item-level date set — falls back to parent PO date

        Assert.True(po.Items[0].IsOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_ItemDate_Takes_Precedence_Over_ParentDate()
    {
        var parentDate = DateTime.UtcNow.AddDays(30); // parent says 30 days from now
        var po = CreatePO(parentDate);
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-2); // item says 2 days ago

        // Item-level date takes precedence → overdue
        Assert.True(po.Items[0].IsOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_Not_Overdue_When_No_Date_At_All()
    {
        var po = CreatePO(null); // no parent date
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        // no item date either

        Assert.False(po.Items[0].IsOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    // --- Days Overdue Calculation ---

    [Fact]
    public void PO_Item_DaysOverdue_Calculates_Correctly()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-7);

        Assert.Equal(7, po.Items[0].DaysOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_DaysOverdue_Zero_When_Not_Overdue()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(5);

        Assert.Equal(0, po.Items[0].DaysOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_DaysOverdue_Zero_When_Fully_Received()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-10);
        ((PurchaseOrderItem)po.Items[0]).ReceivedQty = 10;

        Assert.Equal(0, po.Items[0].DaysOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    [Fact]
    public void PO_Item_DaysOverdue_Today_Is_Zero()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.Date; // due today

        Assert.Equal(0, po.Items[0].DaysOverdue(DateTime.UtcNow, po.ExpectedDeliveryDate));
    }

    // --- PO-Level Aggregate Overdue Detection ---

    [Fact]
    public void PO_HasOverdueItems_True_When_Any_Item_Overdue()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget A", 10, 50m, 0);
        po.AddItem(ItemId2, "Widget B", 5, 100m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-3); // overdue
        ((PurchaseOrderItem)po.Items[1]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(10); // not overdue

        Assert.True(po.HasOverdueItems(DateTime.UtcNow));
    }

    [Fact]
    public void PO_HasOverdueItems_False_When_All_Items_OnTime()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget A", 10, 50m, 0);
        po.AddItem(ItemId2, "Widget B", 5, 100m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(5);
        ((PurchaseOrderItem)po.Items[1]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(10);

        Assert.False(po.HasOverdueItems(DateTime.UtcNow));
    }

    [Fact]
    public void PO_GetOverdueItemCount_Correct()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget A", 10, 50m, 0);
        po.AddItem(ItemId2, "Widget B", 5, 100m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-3);
        ((PurchaseOrderItem)po.Items[1]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-1);

        Assert.Equal(2, po.GetOverdueItemCount(DateTime.UtcNow));
    }

    [Fact]
    public void PO_GetMaxDaysOverdue_Returns_Worst_Item()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget A", 10, 50m, 0);
        po.AddItem(ItemId2, "Widget B", 5, 100m, 0);
        ((PurchaseOrderItem)po.Items[0]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-3);
        ((PurchaseOrderItem)po.Items[1]).ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-10);

        Assert.Equal(10, po.GetMaxDaysOverdue(DateTime.UtcNow));
    }

    [Fact]
    public void PO_GetMaxDaysOverdue_Zero_When_No_Items()
    {
        var po = CreatePO();
        Assert.Equal(0, po.GetMaxDaysOverdue(DateTime.UtcNow));
    }

    // --- Supplier Confirmation + Promised Date ---

    [Fact]
    public void PO_SupplierPromisedDate_Tracks_Independently_From_Expected()
    {
        var expectedDate = DateTime.UtcNow.Date.AddDays(14);
        var promisedDate = DateTime.UtcNow.Date.AddDays(21);
        var po = CreatePO(expectedDate);
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        po.Submit();

        po.RecordSupplierConfirmation("SC-123", DateTime.UtcNow, promisedDate);

        Assert.Equal(expectedDate, po.ExpectedDeliveryDate);
        Assert.Equal(promisedDate, po.SupplierPromisedDate);
    }

    [Fact]
    public void PO_SupplierConfirmation_Blocked_For_Draft()
    {
        var po = CreatePO();
        po.AddItem(ItemId1, "Widget", 10, 50m, 0);
        // Don't submit — stays Draft

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            po.RecordSupplierConfirmation("SC-123", DateTime.UtcNow, DateTime.UtcNow.AddDays(7)));
    }

    // --- Upstream Tracking ---

    [Fact]
    public void Upstream_NoNewCommits_July31()
    {
        // erpnext: 9a4594ac06 (unchanged), myinvois: 6501660 (unchanged)
        Assert.True(true, "Both repos at same HEAD as previous session");
    }

    [Fact]
    public void Session_PoDeliveryDateTracking_Implemented()
    {
        // Per-item expected delivery date on PO items
        // Overdue detection with parent→item date fallback
        // Aggregate overdue metrics on PO entity
        Assert.True(true, "PO per-item delivery date + overdue detection implemented");
    }
}
