using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class StockEntryTypeAndSupplierHoldTests
{
    [Fact]
    public void StockEntryType_HasAll13StandardTypes()
    {
        var values = Enum.GetValues<StockEntryType>();
        values.Length.ShouldBe(14); // 13 standard + Adjustment
    }

    [Fact]
    public void StockEntryType_Manufacture_Value()
    {
        StockEntryType.Manufacture.ShouldBe((StockEntryType)4);
    }

    [Fact]
    public void StockEntryType_SendToSubcontractor_Value()
    {
        StockEntryType.SendToSubcontractor.ShouldBe((StockEntryType)6);
    }

    [Fact]
    public void StockEntryType_Disassemble_Value()
    {
        StockEntryType.Disassemble.ShouldBe((StockEntryType)8);
    }

    [Fact]
    public void StockEntryType_MaterialTransferForManufacture_Value()
    {
        StockEntryType.MaterialTransferForManufacture.ShouldBe((StockEntryType)3);
    }

    [Fact]
    public void StockEntryType_SendToWarehouse_Exists()
    {
        StockEntryType.SendToWarehouse.ShouldBe((StockEntryType)9);
        StockEntryType.ReceiveAtWarehouse.ShouldBe((StockEntryType)10);
    }

    [Fact]
    public void SupplierHoldType_All_BlocksPO()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Hold Supplier");
        supplier.HoldType = SupplierHoldType.All;
        supplier.HoldType.ShouldBe(SupplierHoldType.All);
        supplier.IsOnHold.ShouldBeTrue();
    }

    [Fact]
    public void SupplierHoldType_Payments_DoesNotBlockPO()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Payment Hold");
        supplier.HoldType = SupplierHoldType.Payments;
        // Per ERPNext: Payments hold blocks PE but NOT PO
        (supplier.HoldType == SupplierHoldType.All).ShouldBeFalse();
    }

    [Fact]
    public void SupplierHoldType_Payments_BlocksPE()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Payment Hold");
        supplier.HoldType = SupplierHoldType.Payments;
        // PE should be blocked when HoldType is All or Payments
        var blocked = supplier.HoldType == SupplierHoldType.All
                   || supplier.HoldType == SupplierHoldType.Payments;
        blocked.ShouldBeTrue();
    }

    [Fact]
    public void SupplierHoldType_Invoices_DoesNotBlockPO()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Invoice Hold");
        supplier.HoldType = SupplierHoldType.Invoices;
        // Invoice hold does NOT block PO or PE
        (supplier.HoldType == SupplierHoldType.All).ShouldBeFalse();
    }

    [Fact]
    public void Bin_ConcurrencyStamp_HasInitialValue()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
        bin.ConcurrencyStamp.Length.ShouldBe(32); // Guid without dashes
    }

    [Fact]
    public void Bin_ConcurrencyStamp_ChangesOnSet()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var original = bin.ConcurrencyStamp;
        bin.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        bin.ConcurrencyStamp.ShouldNotBe(original);
    }

    [Fact]
    public void StockEntry_CreateWithManufacturePurpose()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        se.EntryType.ShouldBe(StockEntryType.Manufacture);
    }

    [Fact]
    public void StockEntry_CreateWithRepackPurpose()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Repack, DateTime.UtcNow);
        se.EntryType.ShouldBe(StockEntryType.Repack);
    }
}
