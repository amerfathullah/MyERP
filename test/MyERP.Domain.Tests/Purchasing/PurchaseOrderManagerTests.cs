using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseOrderManagerTests
{
    [Fact]
    public void PO_Cancel_FromClosed_Throws()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        po.Submit();
        po.Close();

        // Per ERPNext (mirrors SalesOrder): a Closed PO must be reopened before cancelling.
        Should.Throw<BusinessException>(() => po.Cancel());
    }

    [Fact]
    public void PO_Cancel_FromSubmitted_Succeeds()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        po.Submit();

        po.Cancel();

        po.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public async Task ValidateCanCancelAsync_ActiveSubcontractingOrder_Throws()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);

        var sco = new SubcontractingOrder(Guid.NewGuid(), po.CompanyId, "SCO-001", DateTime.UtcNow, Guid.NewGuid());
        sco.PurchaseOrderId = po.Id;
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Widget", 1, 1));
        sco.Submit(); // Status = Open — the "submitted" tier the cancel guard checks

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseReceipt>().AsQueryable()));
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice>().AsQueryable()));
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        scoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SubcontractingOrder> { sco }.AsQueryable()));

        await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateCanCancelAsync(po, prRepo, piRepo, scoRepo));
    }

    [Fact]
    public void PO_ReceiptQtyValidation_WithinLimit_Succeeds()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);

        // Should not throw — 50 <= 100 pending
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        manager.ValidateReceiptQty(po, po.Items[0].ItemId, 50);
    }

    [Fact]
    public void PO_ReceiptQtyValidation_ExceedsLimit_Throws()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].ReceivedQty = 80; // 20 remaining

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateReceiptQty(po, itemId, 30));
        ex.Code.ShouldBe("MyERP:08006");
    }

    [Fact]
    public void PO_ReceiptQtyValidation_ExactPending_Succeeds()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].ReceivedQty = 60; // 40 remaining

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        manager.ValidateReceiptQty(po, itemId, 40); // exactly at limit
    }

    [Fact]
    public void PO_BillingQtyValidation_WithinLimit_Succeeds()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].BilledQty = 30;

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        manager.ValidateBillingQty(po, itemId, 60); // 30+60=90 <= 100
    }

    [Fact]
    public void PO_BillingQtyValidation_ExceedsLimit_Throws()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].BilledQty = 80;

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateBillingQty(po, itemId, 30)); // 80+30=110 > 100
        ex.Code.ShouldBe("MyERP:08007");
    }

    [Fact]
    public void PO_BillingQtyValidation_UnknownItem_NoThrow()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        // Unknown itemId should not throw — no matching PO item to validate against
        manager.ValidateBillingQty(po, Guid.NewGuid(), 999);
    }

    [Fact]
    public void PO_BillingQtyValidation_WithConversionFactor_ConvertsToPoUom()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 10, 100, 0); // 10 Box
        po.Items[0].ConversionFactor = 10m; // 1 Box = 10 Nos
        po.Items[0].BilledQty = 4; // 4 Box already billed

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        // Bill 50 Nos with conversionFactor 1 -> 50 * 1 / 10 = 5 Box. 4 + 5 = 9 <= 10.
        manager.ValidateBillingQty(po, itemId, 50, overBillingAllowancePct: 0, conversionFactor: 1m);

        // Bill 70 Nos with conversionFactor 1 -> 70 * 1 / 10 = 7 Box. 4 + 7 = 11 > 10.
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateBillingQty(po, itemId, 70, overBillingAllowancePct: 0, conversionFactor: 1m));
        ex.Code.ShouldBe("MyERP:08007");
    }

    [Fact]
    public void PO_PendingReceiptQty_CalculatesCorrectly()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);
        po.Items[0].ReceivedQty = 40;

        po.Items[0].PendingReceiptQty.ShouldBe(60);
    }

    [Fact]
    public void PO_PendingBillingQty_NeverNegative()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);
        po.Items[0].BilledQty = 120; // Over-billed (shouldn't happen, but guard)

        po.Items[0].PendingBillingQty.ShouldBe(0); // Max(0, ...)
    }

    [Fact]
    public void PO_PerReceived_Uses_MinFormula()
    {
        var po = CreatePO();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        po.AddItem(itemA, "Widget A", 100, 10, 0);
        po.AddItem(itemB, "Widget B", 50, 20, 0);

        po.Items[0].ReceivedQty = 100; // 100% received
        po.Items[1].ReceivedQty = 25;  // 50% received

        // Gotcha #370: SUM(MIN(received_qty, qty)) / SUM(qty) = (100 + 25) / (100 + 50) = 125 / 150 = 83.33%
        po.PerReceived.ShouldBe(83.33m);
    }

    [Fact]
    public void PO_PerBilled_Uses_NetTotal()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0); // NetTotal = 1000
        po.Items[0].BilledQty = 5; // 5 * 100 = 500

        // 500 / 1000 * 100 = 50%
        po.PerBilled.ShouldBe(50m);
    }

    [Fact]
    public async Task PO_ValidateMinOrderQtyAsync_ThrowsWhenBelowMinOrderQty()
    {
        var itemRepo = Substitute.For<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new DomainServices.PurchaseOrderManager(null!, itemRepo, null!);

        var itemId = Guid.NewGuid();
        var item = new MyERP.Inventory.Entities.Item(itemId, Guid.NewGuid(), "ITEM-001", "Raw Material 1", MyERP.Inventory.ItemType.Goods)
        {
            MinOrderQty = 100m
        };

        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<MyERP.Inventory.Entities.Item> { item }.AsQueryable()));

        var po = CreatePO();
        po.AddItem(itemId, "Raw Material 1", 50m, 10m, 0m); // 50 < 100

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateMinOrderQtyAsync(po));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task PO_ValidateMinOrderQtyAsync_ToleratesUomConversionDust()
    {
        var itemRepo = Substitute.For<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new DomainServices.PurchaseOrderManager(null!, itemRepo, null!);

        var itemId = Guid.NewGuid();
        var item = new MyERP.Inventory.Entities.Item(itemId, Guid.NewGuid(), "ITEM-002", "Raw Material 2", MyERP.Inventory.ItemType.Goods)
        {
            MinOrderQty = 2000m
        };

        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<MyERP.Inventory.Entities.Item> { item }.AsQueryable()));

        var po = CreatePO();
        po.AddItem(itemId, "Raw Material 2", 2000m, 10m, 0m);
        // Simulate minor float dust on stock qty
        po.Items[0].ConversionFactor = 0.99999999m;

        await manager.ValidateMinOrderQtyAsync(po);
    }

    [Fact]
    public void Uom_ValidateWholeNumber_ToleratesConversionDust()
    {
        var uom = new MyERP.Inventory.Entities.Uom(Guid.NewGuid(), "Nos")
        {
            MustBeWholeNumber = true
        };

        // 1999.99999 rounds to 2000 at precision 4 -> should NOT throw
        uom.ValidateWholeNumber(1999.99999m);
        uom.ValidateWholeNumber(2000.00001m);

        // 2000.5 is truly fractional -> should throw
        Should.Throw<BusinessException>(() => uom.ValidateWholeNumber(2000.5m));
    }

    [Fact]
    public async Task PO_UpdateSupplierQuotationOrderedQtyAsync_CancelledSQ_Throws()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var sqRepo = Substitute.For<IRepository<SupplierQuotation, Guid>>();

        var sqId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var sq = new SupplierQuotation(sqId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(itemId, 10, 50m);
        sq.Submit();
        sq.Cancel();

        sqRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SupplierQuotation> { sq }.AsQueryable()));

        var po = CreatePO();
        po.SupplierQuotationId = sqId;
        po.AddItem(itemId, "Item 1", 5m, 50m, 0m);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.UpdateSupplierQuotationOrderedQtyAsync(po, sqRepo, reverse: false));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task PO_UpdateSupplierQuotationOrderedQtyAsync_DraftSQ_Throws()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var sqRepo = Substitute.For<IRepository<SupplierQuotation, Guid>>();

        var sqId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var sq = new SupplierQuotation(sqId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(itemId, 10, 50m);

        sqRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SupplierQuotation> { sq }.AsQueryable()));

        var po = CreatePO();
        po.SupplierQuotationId = sqId;
        po.AddItem(itemId, "Item 1", 5m, 50m, 0m);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.UpdateSupplierQuotationOrderedQtyAsync(po, sqRepo, reverse: false));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task PO_UpdateSupplierQuotationOrderedQtyAsync_SubmittedSQ_Succeeds()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var sqRepo = Substitute.For<IRepository<SupplierQuotation, Guid>>();

        var sqId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var sq = new SupplierQuotation(sqId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sq.AddItem(itemId, 10, 50m);
        sq.Submit();

        sqRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SupplierQuotation> { sq }.AsQueryable()));

        var po = CreatePO();
        po.SupplierQuotationId = sqId;
        po.AddItem(itemId, "Item 1", 5m, 50m, 0m);

        await manager.UpdateSupplierQuotationOrderedQtyAsync(po, sqRepo, reverse: false);

        sq.Items[0].OrderedQty.ShouldBe(5m);
        sq.OrderStatus.ShouldBe("Partially Ordered");
    }

    private static PurchaseOrder CreatePO()
    {
        return new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
    }

    [Fact]
    public void ValidateDuplicateItems_WhenAllowed_DoesNotThrow()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 1, 10m, 0m);
        po.AddItem(itemId, "Widget", 2, 10m, 0m);

        manager.ValidateDuplicateItems(po, allowMultipleItems: true);
    }

    [Fact]
    public void ValidateDuplicateItems_WhenDisallowed_DuplicateItem_Throws()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 1, 10m, 0m);
        po.AddItem(itemId, "Widget", 2, 10m, 0m);

        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateDuplicateItems(po, allowMultipleItems: false));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateDuplicateItems_WhenDisallowed_DistinctItems_Succeeds()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget 1", 1, 10m, 0m);
        po.AddItem(Guid.NewGuid(), "Widget 2", 2, 10m, 0m);

        manager.ValidateDuplicateItems(po, allowMultipleItems: false);
    }

    // ========== ERPNext PR #61336: Billed Qty and Conversion Factor Tests ==========

    [Fact]
    public void PO_PendingBillingQty_RecurringConversionFactor_RoundsToZero()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        // 15 boxes where conversion factor = 0.333333333 (1/3)
        po.AddItem(itemId, "Flour", 15m, 100m, 0m);
        var poItem = po.Items.First();
        poItem.ConversionFactor = 0.333333333m;

        // Bill exactly 15 boxes: 15 * 0.333333333 / 0.333333333 = 15
        poItem.BilledQty = 14.999999999m;
        // Without rounding, 15 - 14.999999999 = 0.000000001
        // With Math.Round(..., 4), pending billing is exactly 0
        poItem.PendingBillingQty.ShouldBe(0m);
    }

    [Fact]
    public void PO_ValidateBillingQty_SplitAcrossRows_SummedBeforeConversion()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Bulk Item", 1m, 60m, 0m);
        var poItem = po.Items.First();
        poItem.ConversionFactor = 6m; // 1 Box = 6 units

        // 6 split invoice items, each 1 unit (conversion factor 1m)
        var billingLines = new List<(Guid PurchaseOrderItemId, decimal Quantity, decimal ConversionFactor)>
        {
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m)
        };

        // 6 units * 1 / 6 = 1.0 Box. Exactly covers PO quantity of 1 Box.
        manager.ValidateBillingQty(po, billingLines, 0m);
    }

    [Fact]
    public void PO_ValidateBillingQty_SplitAcrossRows_Exceeds_Throws()
    {
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Bulk Item", 1m, 60m, 0m);
        var poItem = po.Items.First();
        poItem.ConversionFactor = 6m; // 1 Box = 6 units

        // 7 split invoice items, each 1 unit (conversion factor 1m)
        var billingLines = new List<(Guid PurchaseOrderItemId, decimal Quantity, decimal ConversionFactor)>
        {
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m),
            (poItem.Id, 1m, 1m)
        };

        // 7 units * 1 / 6 = 1.1667 Box > 1.0 Box.
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateBillingQty(po, billingLines, 0m));
        ex.Code.ShouldBe("MyERP:08007");
    }
}
