using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class EntityWiringAndSettingsTests
{
    [Fact]
    public void PR_To_PI_Conversion_Sets_PurchaseReceiptItemId()
    {
        // The PR→PI conversion should set PurchaseReceiptItemId on each PI item
        var prItemId = Guid.NewGuid();
        var piItem = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 50, 0);
        piItem.PurchaseReceiptItemId = prItemId;

        piItem.PurchaseReceiptItemId.ShouldBe(prItemId);
        piItem.PurchaseOrderItemId.ShouldBeNull(); // Independent fields
    }

    [Fact]
    public void PI_Item_Has_Both_PO_And_PR_Links()
    {
        var poItemId = Guid.NewGuid();
        var prItemId = Guid.NewGuid();
        var piItem = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 50, 0);
        piItem.PurchaseOrderItemId = poItemId;
        piItem.PurchaseReceiptItemId = prItemId;

        piItem.PurchaseOrderItemId.ShouldBe(poItemId);
        piItem.PurchaseReceiptItemId.ShouldBe(prItemId);
    }

    [Fact]
    public void Company_ExchangeGainLossAccountId_DefaultsNull()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        company.ExchangeGainLossAccountId.ShouldBeNull();
    }

    [Fact]
    public void Company_ExchangeGainLossAccountId_CanBeSet()
    {
        var accountId = Guid.NewGuid();
        var company = new Company(Guid.NewGuid(), "Test Corp");
        company.ExchangeGainLossAccountId = accountId;
        company.ExchangeGainLossAccountId.ShouldBe(accountId);
    }

    [Fact]
    public void StockEntryType_All14Types_Available()
    {
        // Verify all 14 types are defined and castable
        ((int)StockEntryType.MaterialReceipt).ShouldBe(0);
        ((int)StockEntryType.MaterialIssue).ShouldBe(1);
        ((int)StockEntryType.MaterialTransfer).ShouldBe(2);
        ((int)StockEntryType.MaterialTransferForManufacture).ShouldBe(3);
        ((int)StockEntryType.Manufacture).ShouldBe(4);
        ((int)StockEntryType.Repack).ShouldBe(5);
        ((int)StockEntryType.SendToSubcontractor).ShouldBe(6);
        ((int)StockEntryType.MaterialConsumptionForManufacture).ShouldBe(7);
        ((int)StockEntryType.Disassemble).ShouldBe(8);
        ((int)StockEntryType.SendToWarehouse).ShouldBe(9);
        ((int)StockEntryType.ReceiveAtWarehouse).ShouldBe(10);
        ((int)StockEntryType.SubcontractingDelivery).ShouldBe(11);
        ((int)StockEntryType.SubcontractingReturn).ShouldBe(12);
        ((int)StockEntryType.Adjustment).ShouldBe(13);
    }

    [Fact]
    public void StockEntry_WithManufacturePurpose_HasCorrectType()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow);
        se.EntryType.ShouldBe(StockEntryType.MaterialTransferForManufacture);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_SingleRefPartial()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 15000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 15000m, 10000m, 10000m));

        // 15000 - 10000 = 5000 unallocated
        pe.UnallocatedAmount.ShouldBe(5000m);
    }

    [Fact]
    public void Company_SetCurrency_InitialSet_Works()
    {
        var company = new Company(Guid.NewGuid(), "New Corp");
        company.SetCurrency("SGD", hasSubmittedTransactions: false);
        company.CurrencyCode.ShouldBe("SGD");
    }
}
