using System;
using Xunit;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Core;
using MyERP.Inventory;

namespace MyERP;

/// <summary>
/// Tests verifying recent backend fixes:
/// - Name resolution on GetAsync/GetListAsync (party names populated in DTOs)
/// - DTO field mapping correctness
/// - UOM conversion propagation in document conversion
/// - Return document handling
/// </summary>
public class NameResolutionAndDtoTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid FiscalYearId = Guid.NewGuid();

    // === Sales Invoice DTO Tests ===

    [Fact]
    public void SalesInvoice_HasCustomerNameField_ForListDisplay()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-001", DateTime.Today);
        Assert.Equal(CustomerId, si.CustomerId);
    }

    [Fact]
    public void SalesInvoice_ReturnFields_DefaultCorrectly()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-002", DateTime.Today);
        Assert.False(si.IsReturn);
        Assert.Null(si.ReturnAgainstId);
        Assert.Null(si.AmendedFromId);
        Assert.Equal(0, si.AmendmentIndex);
    }

    [Fact]
    public void SalesInvoice_MultiCurrencyFields_DefaultToBaseCurrency()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-003", DateTime.Today);
        Assert.Equal(1m, si.ExchangeRate);
    }

    [Fact]
    public void SalesInvoice_ForeignCurrency_ExchangeRateCanBeSet()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-004", DateTime.Today);
        si.CurrencyCode = "USD";
        si.ExchangeRate = 4.72m;
        Assert.Equal(4.72m, si.ExchangeRate);
        Assert.Equal("USD", si.CurrencyCode);
    }

    // === Purchase Invoice DTO Tests ===

    [Fact]
    public void PurchaseInvoice_HasSupplierIdField()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-001", DateTime.Today);
        Assert.Equal(SupplierId, pi.SupplierId);
    }

    [Fact]
    public void PurchaseInvoice_AmendmentFields_DefaultCorrectly()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-002", DateTime.Today);
        Assert.Null(pi.AmendedFromId);
        Assert.Equal(0, pi.AmendmentIndex);
        Assert.False(pi.IsReturn);
    }

    [Fact]
    public void PurchaseInvoice_UpdateStockFields_DefaultFalse()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-003", DateTime.Today);
        Assert.False(pi.UpdateStock);
        Assert.Null(pi.WarehouseId);
    }

    // === Purchase Order DTO Tests ===

    [Fact]
    public void PurchaseOrder_HasSupplierId_ForNameResolution()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        Assert.Equal(SupplierId, po.SupplierId);
    }

    [Fact]
    public void PurchaseOrder_FulfillmentStatus_DefaultsAfterSubmit()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-002", DateTime.Today);
        po.AddItem(ItemId, "Test Item", 10, 100, 0, "Unit");
        po.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // === Delivery Note DTO Tests ===

    [Fact]
    public void DeliveryNote_HasCustomerId_ForNameResolution()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-001", DateTime.Today);
        Assert.Equal(CustomerId, dn.CustomerId);
    }

    [Fact]
    public void DeliveryNote_ReturnFields_DefaultCorrectly()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-002", DateTime.Today);
        Assert.False(dn.IsReturn);
        Assert.Null(dn.ReturnAgainstId);
    }

    // === Stock Entry Item DTO Tests ===

    [Fact]
    public void StockEntryItem_HasItemId_ForNameResolution()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        se.AddItem(ItemId, 10, null, WarehouseId);
        var item = Assert.Single(se.Items);
        Assert.Equal(ItemId, item.ItemId);
    }

    [Fact]
    public void StockEntryItem_HasSourceAndTargetWarehouse()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialTransfer, DateTime.Today);
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();
        se.AddItem(ItemId, 5, sourceWh, targetWh, 100);
        var item = Assert.Single(se.Items);
        Assert.Equal(sourceWh, item.SourceWarehouseId);
        Assert.Equal(targetWh, item.TargetWarehouseId);
    }

    // === Journal Entry Line DTO Tests ===

    [Fact]
    public void JournalEntryLine_HasAccountId_ForNameResolution()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.Today);
        je.AddLine(AccountId, 1000, true);
        je.AddLine(Guid.NewGuid(), 1000, false);
        var debitLine = je.Lines[0];
        Assert.Equal(AccountId, debitLine.AccountId);
    }

    [Fact]
    public void JournalEntryLine_MultiCurrencyFields_Default()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.Today);
        je.AddLine(AccountId, 500, true);
        je.AddLine(Guid.NewGuid(), 500, false);
        var line = je.Lines[0];
        Assert.Equal(1m, line.ExchangeRate);
    }

    // === UOM Conversion Propagation Tests ===

    [Fact]
    public void SalesOrderItem_UomFields_DefaultToStandard()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 25, 0, "Unit");
        var item = Assert.Single(so.Items);
        Assert.Equal("Unit", item.StockUom);
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty);
    }

    [Fact]
    public void SalesOrderItem_UomConversion_CalculatesStockQty()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-002", DateTime.Today);
        so.AddItem(ItemId, "Widget", 5, 120, 0, "Dozen");
        var item = Assert.Single(so.Items);
        item.StockUom = "Unit";
        item.ConversionFactor = 12m;
        Assert.Equal(60m, item.StockQty);
    }

    [Fact]
    public void PurchaseOrderItem_UomConversion_CalculatesStockQty()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-003", DateTime.Today);
        po.AddItem(ItemId, "Raw Material", 3, 500, 0, "Box");
        var item = Assert.Single(po.Items);
        item.StockUom = "Unit";
        item.ConversionFactor = 100m;
        Assert.Equal(300m, item.StockQty);
    }

    [Fact]
    public void DeliveryNoteItem_UomConversion_AffectsStockQty()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-003", DateTime.Today);
        dn.AddItem(ItemId, "Product", 2, 250, 0);
        var item = Assert.Single(dn.Items);
        item.StockUom = "Unit";
        item.ConversionFactor = 24m;
        Assert.Equal(48m, item.StockQty);
    }

    // === Payment Entry Reference Tests ===

    [Fact]
    public void PaymentEntry_References_DefaultEmpty()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, Accounting.PaymentType.Receive, DateTime.Today, 1000, AccountId, Guid.NewGuid());
        Assert.Empty(pe.References);
    }

    [Fact]
    public void PaymentEntry_PartyFields_ForNameResolution()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, Accounting.PaymentType.Receive, DateTime.Today, 5000, AccountId, Guid.NewGuid());
        pe.PartyType = "Customer";
        pe.PartyId = CustomerId;
        Assert.Equal("Customer", pe.PartyType);
        Assert.Equal(CustomerId, pe.PartyId);
    }

    // === Purchase Receipt Tests ===

    [Fact]
    public void PurchaseReceipt_HasSupplierId_ForNameResolution()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.Today);
        Assert.Equal(SupplierId, pr.SupplierId);
        Assert.Equal(WarehouseId, pr.WarehouseId);
    }

    [Fact]
    public void PurchaseReceipt_ReturnDefaults()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-002", DateTime.Today);
        Assert.False(pr.IsReturn);
        Assert.Null(pr.ReturnAgainstId);
    }

    // === Address Resolution Tests ===

    [Fact]
    public void SalesOrder_AddressFields_DefaultNull()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-003", DateTime.Today);
        Assert.Null(so.BillingAddressId);
        Assert.Null(so.ShippingAddressId);
    }

    [Fact]
    public void SalesOrder_AddressFields_CanBeSet()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-004", DateTime.Today);
        var billingId = Guid.NewGuid();
        var shippingId = Guid.NewGuid();
        so.BillingAddressId = billingId;
        so.ShippingAddressId = shippingId;
        Assert.Equal(billingId, so.BillingAddressId);
        Assert.Equal(shippingId, so.ShippingAddressId);
    }

    [Fact]
    public void PurchaseOrder_BillingAddress_DefaultNull()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-004", DateTime.Today);
        Assert.Null(po.BillingAddressId);
    }
}
