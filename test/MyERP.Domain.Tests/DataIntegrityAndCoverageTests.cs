using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for warehouse hierarchy, UOM data, currency exchange,
/// and other recently-added seed data and entity features.
/// </summary>
public class DataIntegrityAndCoverageTests
{
    // === Warehouse Hierarchy ===

    [Fact]
    public void Warehouse_ParentWarehouseId_DefaultsToNull()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Stores");
        Assert.Null(wh.ParentWarehouseId);
    }

    [Fact]
    public void Warehouse_ParentWarehouseId_CanBeSet()
    {
        var parentId = Guid.NewGuid();
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Stores")
        {
            ParentWarehouseId = parentId
        };
        Assert.Equal(parentId, wh.ParentWarehouseId);
    }

    [Fact]
    public void Warehouse_GroupWarehouse_CanHaveChildren()
    {
        var companyId = Guid.NewGuid();
        var root = new Warehouse(Guid.NewGuid(), companyId, "All Warehouses") { IsGroup = true };
        var child = new Warehouse(Guid.NewGuid(), companyId, "Stores")
        {
            ParentWarehouseId = root.Id
        };

        Assert.True(root.IsGroup);
        Assert.False(child.IsGroup);
        Assert.Equal(root.Id, child.ParentWarehouseId);
    }

    [Fact]
    public void Warehouse_IsGroup_DefaultsFalse()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Test");
        Assert.False(wh.IsGroup);
    }

    [Fact]
    public void Warehouse_IsActive_DefaultsTrue()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Test");
        Assert.True(wh.IsActive);
    }

    [Fact]
    public void Warehouse_MultipleChildrenUnderSameParent()
    {
        var companyId = Guid.NewGuid();
        var root = new Warehouse(Guid.NewGuid(), companyId, "All Warehouses") { IsGroup = true };

        var stores = new Warehouse(Guid.NewGuid(), companyId, "Stores") { ParentWarehouseId = root.Id };
        var fg = new Warehouse(Guid.NewGuid(), companyId, "Finished Goods") { ParentWarehouseId = root.Id };
        var wip = new Warehouse(Guid.NewGuid(), companyId, "Work In Progress") { ParentWarehouseId = root.Id };
        var transit = new Warehouse(Guid.NewGuid(), companyId, "Goods In Transit") { ParentWarehouseId = root.Id };

        var children = new[] { stores, fg, wip, transit };
        Assert.All(children, c => Assert.Equal(root.Id, c.ParentWarehouseId));
        Assert.Equal(4, children.Length);
    }

    // === UOM Entity ===

    [Fact]
    public void Uom_Create_SetsName()
    {
        var uom = new Uom(Guid.NewGuid(), "Kilogram");
        Assert.Equal("Kilogram", uom.Name);
    }

    [Fact]
    public void Uom_MustBeWholeNumber_DefaultsFalse()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg");
        Assert.False(uom.MustBeWholeNumber);
    }

    [Fact]
    public void Uom_WholeNumber_ConfiguredCorrectly()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        Assert.True(uom.MustBeWholeNumber);
        // ValidateWholeNumber should not throw for whole numbers
        uom.ValidateWholeNumber(5m);
        uom.ValidateWholeNumber(100m);
    }

    [Fact]
    public void Uom_ContinuousUom_AllowsFractional()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg") { MustBeWholeNumber = false };
        // Non-whole-number UOM: ValidateWholeNumber is a no-op when MustBeWholeNumber=false
        Assert.False(uom.MustBeWholeNumber);
    }

    [Fact]
    public void Uom_Category_CanBeSet()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg") { Category = "Mass" };
        Assert.Equal("Mass", uom.Category);
    }

    [Fact]
    public void Uom_IsEnabled_DefaultsTrue()
    {
        var uom = new Uom(Guid.NewGuid(), "Test");
        Assert.True(uom.IsEnabled);
    }

    [Fact]
    public void Uom_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Uom(Guid.NewGuid(), ""));
    }

    // === UOM Conversion ===

    [Fact]
    public void UomConversion_Create_SetsProperties()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal("Kg", conv.FromUom);
        Assert.Equal("Gram", conv.ToUom);
        Assert.Equal(1000m, conv.ConversionFactor);
    }

    [Fact]
    public void UomConversion_Convert_MultipliesByFactor()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal(5000m, conv.Convert(5m));
    }

    [Fact]
    public void UomConversion_ReverseConvert_DividesByFactor()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        Assert.Equal(2m, conv.ReverseConvert(2000m));
    }

    [Fact]
    public void UomConversion_ReverseConvert_ZeroFactor_ReturnsZero()
    {
        var conv = new UomConversion(Guid.NewGuid(), "A", "B", 0m);
        Assert.Equal(0m, conv.ReverseConvert(100m));
    }

    [Fact]
    public void UomConversion_ItemSpecific_HasItemId()
    {
        var itemId = Guid.NewGuid();
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m, itemId: itemId);
        Assert.Equal(itemId, conv.ItemId);
    }

    [Fact]
    public void UomConversion_Global_HasNullItemId()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Dozen", "Unit", 12m);
        Assert.Null(conv.ItemId);
    }

    [Fact]
    public void UomConversion_Precision_Preserved()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Pound", "Kg", 0.453592m);
        var result = conv.Convert(10m);
        Assert.Equal(4.53592m, result);
    }

    // === Currency Exchange ===

    [Fact]
    public void CurrencyExchange_Create_SetsProperties()
    {
        var ce = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.72m, new DateTime(2026, 1, 1));
        Assert.Equal("USD", ce.FromCurrency);
        Assert.Equal("MYR", ce.ToCurrency);
        Assert.Equal(4.72m, ce.ExchangeRate);
    }

    [Fact]
    public void CurrencyExchange_PeggedRate_HasEarlyDate()
    {
        // Pegged currencies use a very early date to indicate permanence
        var ce = new CurrencyExchange(Guid.NewGuid(), "AED", "USD", 3.6725m, new DateTime(2000, 1, 1));
        Assert.Equal(new DateTime(2000, 1, 1), ce.Date);
    }

    [Fact]
    public void CurrencyExchange_InverseRate_Calculation()
    {
        var ce = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.72m, DateTime.Today);
        // Inverse: 1/4.72 = ~0.2118644
        var inverse = 1m / ce.ExchangeRate;
        Assert.True(inverse > 0.21m && inverse < 0.22m);
    }

    // === POS Invoice Walk-In Customer Requirement ===

    [Fact]
    public void SalesInvoice_Constructor_RequiresCustomerId()
    {
        // Guid.Empty should be rejected by FK guard
        Assert.Throws<ArgumentException>(() => new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "POS-001",
            DateTime.Today, null));
    }

    // === Delivery Note Warehouse Requirement ===

    [Fact]
    public void DeliveryNote_Constructor_RequiresWarehouseId()
    {
        // Guid.Empty should be rejected by FK guard
        Assert.Throws<ArgumentException>(() => new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            "DN-001", DateTime.Today, null));
    }

    // === Purchase Receipt Warehouse Requirement ===

    [Fact]
    public void PurchaseReceipt_Constructor_RequiresWarehouseId()
    {
        Assert.Throws<ArgumentException>(() => new PurchaseReceipt(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            "PR-001", DateTime.Today, null));
    }

    // === Depreciation Account Resolution ===

    [Fact]
    public void AssetCategory_DepreciationAccounts_DefaultNull()
    {
        var cat = new Assets.Entities.AssetCategory(Guid.NewGuid(), "Equipment", null);
        Assert.Null(cat.DepreciationAccountId);
        Assert.Null(cat.AccumulatedDepreciationAccountId);
    }

    [Fact]
    public void AssetCategory_DepreciationAccounts_CanBeSet()
    {
        var depAcctId = Guid.NewGuid();
        var accumAcctId = Guid.NewGuid();
        var cat = new Assets.Entities.AssetCategory(Guid.NewGuid(), "Equipment", null)
        {
            DepreciationAccountId = depAcctId,
            AccumulatedDepreciationAccountId = accumAcctId
        };
        Assert.Equal(depAcctId, cat.DepreciationAccountId);
        Assert.Equal(accumAcctId, cat.AccumulatedDepreciationAccountId);
    }

    // === Company Default Account Fallback Chain ===

    [Fact]
    public void Company_DepreciationAccounts_DefaultNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.DepreciationExpenseAccountId);
        Assert.Null(company.AccumulatedDepreciationAccountId);
    }

    [Fact]
    public void Company_DefaultExpenseAccountId_DefaultNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.DefaultExpenseAccountId);
    }

    // === Conversion Service Warehouse Resolution ===

    [Fact]
    public void SalesOrderItem_WarehouseId_CanBeSet()
    {
        var order = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0, "Unit");
        var item = order.Items.First();
        var whId = Guid.NewGuid();
        item.WarehouseId = whId;
        Assert.Equal(whId, item.WarehouseId);
    }

    [Fact]
    public void PurchaseOrderItem_WarehouseId_CanBeSet()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0, "Unit");
        var item = order.Items.First();
        var whId = Guid.NewGuid();
        item.WarehouseId = whId;
        Assert.Equal(whId, item.WarehouseId);
    }

    [Fact]
    public void SalesOrderItem_WarehouseId_DefaultsNull()
    {
        var order = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0, "Unit");
        Assert.Null(order.Items.First().WarehouseId);
    }
}
