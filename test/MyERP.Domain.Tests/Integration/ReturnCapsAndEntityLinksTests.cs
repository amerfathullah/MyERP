using System;
using System.Collections.Generic;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

public class ReturnCapsAndEntityLinksTests
{
    // --- Return Qty Validation ---

    [Fact]
    public void DeliveryNote_ReturnAgainstId_CanBeSet()
    {
        var originalId = Guid.NewGuid();
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-R001", DateTime.UtcNow);
        dn.IsReturn = true;
        dn.ReturnAgainstId = originalId;
        dn.ReturnAgainstId.ShouldBe(originalId);
    }

    [Fact]
    public void PurchaseReceipt_ReturnAgainstId_CanBeSet()
    {
        var originalId = Guid.NewGuid();
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PR-R001", DateTime.UtcNow);
        pr.IsReturn = true;
        pr.ReturnAgainstId = originalId;
        pr.ReturnAgainstId.ShouldBe(originalId);
    }

    [Fact]
    public void ReturnQtyExceedsOriginal_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.ReturnQtyExceedsOriginal.ShouldBe("MyERP:08004");
    }

    // --- StockEntry WorkOrderId ---

    [Fact]
    public void StockEntry_WorkOrderId_DefaultsNull()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        se.WorkOrderId.ShouldBeNull();
    }

    [Fact]
    public void StockEntry_WorkOrderId_CanBeSet()
    {
        var woId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow);
        se.WorkOrderId = woId;
        se.WorkOrderId.ShouldBe(woId);
    }

    // --- PurchaseInvoiceItem → PurchaseReceiptItemId ---

    [Fact]
    public void PurchaseInvoiceItem_PurchaseReceiptItemId_DefaultsNull()
    {
        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 50, 0);
        item.PurchaseReceiptItemId.ShouldBeNull();
    }

    [Fact]
    public void PurchaseInvoiceItem_PurchaseReceiptItemId_CanBeSet()
    {
        var prItemId = Guid.NewGuid();
        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 50, 0);
        item.PurchaseReceiptItemId = prItemId;
        item.PurchaseReceiptItemId.ShouldBe(prItemId);
    }

    // --- Company Currency Guard ---

    [Fact]
    public void Company_SetCurrency_WhenNoTransactions_Succeeds()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        company.SetCurrency("USD", hasSubmittedTransactions: false);
        company.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void Company_SetCurrency_SameCurrency_AlwaysSucceeds()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        company.CurrencyCode = "MYR";
        // Even with transactions, same currency should work
        company.SetCurrency("MYR", hasSubmittedTransactions: true);
        company.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void Company_SetCurrency_DifferentCurrency_WithTransactions_Throws()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");
        company.CurrencyCode = "MYR";

        var ex = Should.Throw<BusinessException>(() =>
            company.SetCurrency("USD", hasSubmittedTransactions: true));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyCurrencyLocked);
    }

    [Fact]
    public void CompanyCurrencyLocked_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.CompanyCurrencyLocked.ShouldBe("MyERP:00003");
    }

    // --- PaymentEntry UnallocatedAmount ---

    [Fact]
    public void PaymentEntry_UnallocatedAmount_FullyAllocatedSingleInvoice()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstInvoiceId = Guid.NewGuid();
        pe.AgainstInvoiceType = "SalesInvoice";

        // Legacy single-invoice: fully allocated
        pe.UnallocatedAmount.ShouldBe(0m);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_NoAllocation()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 5000m, Guid.NewGuid(), Guid.NewGuid());
        // No invoice linked, no references → entire amount is unallocated
        pe.UnallocatedAmount.ShouldBe(5000m);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_PartialMultiRef()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 10000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 10000m, 6000m, 6000m));
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 5000m, 2000m, 2000m));

        // 10000 - (6000 + 2000) = 2000 unallocated
        pe.UnallocatedAmount.ShouldBe(2000m);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_FullyAllocatedMultiRef()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 8000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 8000m, 5000m, 5000m));
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 5000m, 3000m, 3000m));

        pe.UnallocatedAmount.ShouldBe(0m);
    }
}
