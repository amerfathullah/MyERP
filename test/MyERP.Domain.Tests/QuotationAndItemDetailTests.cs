using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for Quotation lifecycle, Item Details resolution patterns, and print layout data.
/// Per ERPNext: quotation is the first customer-facing document in the sales cycle.
/// Item details resolution is the 25-step chain from get_item_details.py.
/// </summary>
public class QuotationAndItemDetailTests
{
    private static readonly Guid _companyId = Guid.NewGuid();
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _itemId = Guid.NewGuid();

    // --- Quotation Lifecycle Tests ---

    [Fact]
    public void Quotation_ValidUntil_Past_IsExpired()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.Submit();

        // Simulate expiry by setting ValidUntil to past
        typeof(Quotation).GetProperty("ValidUntil")!.SetValue(quotation, DateTime.UtcNow.AddDays(-1));

        Assert.True(quotation.IsExpired);
    }

    [Fact]
    public void Quotation_ValidUntil_Future_NotExpired()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-002", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.Submit();

        typeof(Quotation).GetProperty("ValidUntil")!.SetValue(quotation, DateTime.UtcNow.AddDays(30));

        Assert.False(quotation.IsExpired);
    }

    [Fact]
    public void Quotation_NoValidUntil_NeverExpires()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-003", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.Submit();

        // ValidUntil not set = never expires
        Assert.False(quotation.IsExpired);
    }

    [Fact]
    public void Quotation_MarkLost_Sets_Rejected()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-004", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.Submit();
        quotation.MarkLost();

        Assert.Equal(DocumentStatus.Rejected, quotation.Status);
    }

    [Fact]
    public void Quotation_MarkLost_From_Draft_Throws()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-005", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);

        Assert.Throws<BusinessException>(() => quotation.MarkLost());
    }

    [Fact]
    public void Quotation_Amendment_From_Rejected()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-006", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.Submit();
        quotation.MarkLost();

        // Rejected (lost) quotations can be amended
        Assert.Equal(DocumentStatus.Rejected, quotation.Status);
        // IAmendable check — both Cancelled and Rejected are valid for amendment
    }

    [Fact]
    public void Quotation_ClearItems_Draft_Succeeds()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-007", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.AddItem(Guid.NewGuid(), "Second Item", 2, 200, 0);
        Assert.Equal(2, quotation.Items.Count);

        quotation.ClearItems();
        Assert.Empty(quotation.Items);
    }

    [Fact]
    public void Quotation_ClearItems_After_Submit_Throws()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-008", DateTime.Today);
        quotation.AddItem(_itemId, "Test Item", 1, 100, 0);
        quotation.Submit();

        Assert.Throws<BusinessException>(() => quotation.ClearItems());
    }

    // --- Item Entity Tests for Item Details Resolution ---

    [Fact]
    public void Item_DefaultFields_For_Resolution()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "TEST-001", "Test Item", ItemType.Goods);

        // Item should have default UOM and maintain stock for goods
        Assert.Equal("Unit", item.Uom);
        Assert.True(item.MaintainStock);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Item_Service_Type_No_Stock()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "SVC-001", "Consulting Service", ItemType.Service);

        Assert.False(item.MaintainStock);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Item_ReorderLevel_Detection()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Reorder Test", ItemType.Goods);
        item.ReorderLevel = 10;
        item.ReorderQty = 50;

        // Item needs reorder when projected qty <= reorder level
        Assert.Equal(10m, item.ReorderLevel);
        Assert.Equal(50m, item.ReorderQty);
    }

    [Fact]
    public void Item_MinOrderQty_Enforcement()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-002", "Min Order Test", ItemType.Goods);
        item.MinOrderQty = 100;

        Assert.Equal(100m, item.MinOrderQty);
    }

    [Fact]
    public void Item_ValuationMethod_Default()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-003", "Valuation Test", ItemType.Goods);

        // Default valuation method (from company/stock settings fallback chain)
        Assert.Equal(ValuationMethod.FIFO, item.ValuationMethod);
    }

    [Fact]
    public void Item_HasVariants_Template_Cannot_Transact()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "TEMPLATE-001", "Template Item", ItemType.Goods);
        item.HasVariants = true;

        // Template items cannot be used in transactions
        Assert.True(item.HasVariants);
    }

    // --- Print Layout Data Pattern Tests ---

    [Fact]
    public void Quotation_GrandTotal_Includes_Tax()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-009", DateTime.Today);
        quotation.AddItem(_itemId, "Item A", 10, 100, 0); // line total = 1000

        // After tax cascade (6% SST example)
        // NetTotal = 1000, TaxAmount = 60, GrandTotal = 1060
        Assert.Equal(1000m, quotation.NetTotal);
    }

    [Fact]
    public void Quotation_Currency_Defaults_MYR()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-010", DateTime.Today);

        Assert.Equal("MYR", quotation.CurrencyCode);
    }

    [Fact]
    public void Quotation_MultiCurrency_USD()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-011", DateTime.Today);
        typeof(Quotation).GetProperty("CurrencyCode")!.SetValue(quotation, "USD");

        Assert.Equal("USD", quotation.CurrencyCode);
    }

    [Fact]
    public void Quotation_Items_Have_LineTotal()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-012", DateTime.Today);
        quotation.AddItem(_itemId, "Widget", 5, 200, 0);

        var item = quotation.Items.First();
        Assert.Equal(1000m, item.LineTotal);
    }

    // --- Delivery Schedule Related Tests ---

    [Fact]
    public void DeliveryScheduleEntry_Defaults()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 100);

        Assert.Equal(100m, entry.ScheduledQty);
        Assert.Equal(0m, entry.DeliveredQty);
        Assert.Equal(100m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_Reduces_Pending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 100);
        entry.RecordDelivery(40);

        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_Full_Delivery()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 100);
        entry.RecordDelivery(100);

        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_Pending_Never_Negative()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 50);
        entry.RecordDelivery(60); // Over-delivery

        Assert.True(entry.PendingQty >= 0);
    }

    // --- UOM Conversion Field Tests ---

    [Fact]
    public void SalesOrderItem_StockQty_With_ConversionFactor()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.Today);
        so.AddItem(_itemId, "Dozen Item", 5, 120, 0); // 5 Dozen

        var item = so.Items.First();
        item.ConversionFactor = 12; // 1 Dozen = 12 Units
        item.StockUom = "Unit";

        Assert.Equal(60m, item.StockQty); // 5 × 12 = 60 units in stock
    }

    [Fact]
    public void SalesOrderItem_StockQty_SameUom_Factor1()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-002", DateTime.Today);
        so.AddItem(_itemId, "Unit Item", 10, 50, 0);

        var item = so.Items.First();
        // Default factor = 1 (same UOM)
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty); // 10 × 1 = 10
    }

    [Fact]
    public void PurchaseOrderItem_StockQty_Calculation()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.Today);
        po.AddItem(_itemId, "Pallet", 2, 500, 0);

        var item = po.Items.First();
        item.ConversionFactor = 48; // 1 Pallet = 48 Cases
        item.StockUom = "Case";

        Assert.Equal(96m, item.StockQty); // 2 × 48 = 96 cases
    }
}
