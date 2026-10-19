using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class PurchasingUomConversionAndBillingTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    [Fact]
    public async Task ValidateAgainstPurchaseOrderAsync_WhenPrItemInDifferentUom_NormalizesBeforeValidation()
    {
        // PO ordered: 10 Boxes (each Box = 10 Units, ConversionFactor = 10) -> Total = 100 Units
        // Received so far: 5 Boxes (Pending = 5 Boxes)
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.UtcNow.Date.AddDays(-2));
        po.AddItem(ItemId, "Widget", 10, 100m, 0m, uom: "Box");
        var poItem = po.Items.First();
        poItem.ConversionFactor = 10m;
        poItem.ReceivedQty = 5; // 5 Boxes received, 5 Boxes pending

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        poRepo.GetAsync(po.Id).Returns(po);
        poRepo.GetQueryableAsync().Returns(new List<PurchaseOrder> { po }.AsQueryable());

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        var prManager = new PurchaseReceiptManager(poRepo, prRepo);

        // Case 1: PR receives 40 Units (ConversionFactor = 1) -> 40 / 10 = 4 Boxes <= 5 Boxes pending -> Allowed
        var prAllowed = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.UtcNow.Date);
        prAllowed.PurchaseOrderId = po.Id;
        prAllowed.AddItem(ItemId, "Widget", 40, 10m, 0m, uom: "Unit", purchaseOrderItemId: poItem.Id);
        prAllowed.Items.First().ConversionFactor = 1m;

        await prManager.ValidateAgainstPurchaseOrderAsync(prAllowed);

        // Case 2: PR receives 60 Units (ConversionFactor = 1) -> 60 / 10 = 6 Boxes > 5 Boxes pending -> Over-receipt throws
        var prExceeded = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-002", DateTime.UtcNow.Date);
        prExceeded.PurchaseOrderId = po.Id;
        prExceeded.AddItem(ItemId, "Widget", 60, 10m, 0m, uom: "Unit", purchaseOrderItemId: poItem.Id);
        prExceeded.Items.First().ConversionFactor = 1m;

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await prManager.ValidateAgainstPurchaseOrderAsync(prExceeded));
        ex.Code.ShouldBe("MyERP:08006");
    }

    [Fact]
    public async Task UpdateLinkedPurchaseReceiptBillingAsync_WhenPiInDifferentUom_ConvertsToPrUom()
    {
        // PR received: 10 Boxes (ConversionFactor = 10)
        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.UtcNow.Date);
        pr.AddItem(ItemId, "Widget", 10, 100m, 0m, uom: "Box");
        var prItem = pr.Items.First();
        prItem.ConversionFactor = 10m;
        prItem.BilledQty = 0;

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.GetQueryableAsync().Returns(new List<PurchaseReceipt> { pr }.AsQueryable());

        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IRepository<PurchaseReceipt, Guid>)).Returns(prRepo);

        var piManager = new PurchaseInvoiceManager(null!, null!, null!)
        {
            LazyServiceProvider = new AbpLazyServiceProvider(sp)
        };

        // Invoice bills 30 Units (ConversionFactor = 1) -> in PR UOM: (30 * 1) / 10 = 3 Boxes
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PINV-001", DateTime.UtcNow.Date);
        pi.AddItem(ItemId, "Widget", 30, 10m, 0m, uom: "Unit");
        var piItem = pi.Items.First();
        piItem.ConversionFactor = 1m;
        piItem.PurchaseReceiptItemId = prItem.Id;

        await piManager.UpdateLinkedPurchaseReceiptBillingAsync(pi, reverse: false);

        prItem.BilledQty.ShouldBe(3m);

        // Cancel reverses 3 Boxes
        await piManager.UpdateLinkedPurchaseReceiptBillingAsync(pi, reverse: true);
        prItem.BilledQty.ShouldBe(0m);
    }

    [Fact]
    public async Task UpdateLinkedPurchaseReceiptBillingAsync_WhenDebitNote_ReducesPrBilledQtyAndVariance()
    {
        // PR received: 10 Boxes at rate 100
        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.UtcNow.Date);
        pr.AddItem(ItemId, "Widget", 10, 100m, 0m, uom: "Box");
        var prItem = pr.Items.First();
        prItem.ConversionFactor = 10m;
        prItem.BilledQty = 8m; // previously billed 8 Boxes
        prItem.AmountDifferenceWithPurchaseInvoice = 80m;

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        prRepo.GetQueryableAsync().Returns(new List<PurchaseReceipt> { pr }.AsQueryable());

        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IRepository<PurchaseReceipt, Guid>)).Returns(prRepo);

        var piManager = new PurchaseInvoiceManager(null!, null!, null!)
        {
            LazyServiceProvider = new AbpLazyServiceProvider(sp)
        };

        // Debit Note for 2 Boxes at rate 110 (variance = (110 - 100) * 2 = 20)
        var debitNote = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "DN-001", DateTime.UtcNow.Date);
        debitNote.IsDebitNote = true;
        debitNote.AddItem(ItemId, "Widget", 2, 110m, 0m, uom: "Box");
        var dnItem = debitNote.Items.First();
        dnItem.ConversionFactor = 10m;
        dnItem.PurchaseReceiptItemId = prItem.Id;

        // Submit debit note -> reduces billed qty: 8 - 2 = 6, variance: 80 - 20 = 60
        await piManager.UpdateLinkedPurchaseReceiptBillingAsync(debitNote, reverse: false);
        prItem.BilledQty.ShouldBe(6m);
        prItem.AmountDifferenceWithPurchaseInvoice.ShouldBe(60m);

        // Cancel debit note -> restores billed qty: 6 + 2 = 8, variance: 60 + 20 = 80
        await piManager.UpdateLinkedPurchaseReceiptBillingAsync(debitNote, reverse: true);
        prItem.BilledQty.ShouldBe(8m);
        prItem.AmountDifferenceWithPurchaseInvoice.ShouldBe(80m);
    }
}
