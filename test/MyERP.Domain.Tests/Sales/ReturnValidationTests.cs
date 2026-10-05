using System;
using System.Linq;
using MyERP.Core;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Sales;

public class ReturnValidationTests
{
    private static SalesInvoice CreateInvoice(decimal qty = 10m)
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-001", DateTime.UtcNow);
        invoice.AddItem(Guid.NewGuid(), "Widget", qty, 100m, 6m);
        return invoice;
    }

    [Fact]
    public void SalesInvoice_IsReturn_DefaultFalse()
    {
        var invoice = CreateInvoice();
        invoice.IsReturn.ShouldBeFalse();
    }

    [Fact]
    public void SalesInvoice_CanSetIsReturn()
    {
        var invoice = CreateInvoice();
        invoice.IsReturn = true;
        invoice.IsReturn.ShouldBeTrue();
    }

    [Fact]
    public void SalesInvoice_ReturnAgainstId_SetCorrectly()
    {
        var originalId = Guid.NewGuid();
        var invoice = CreateInvoice();
        invoice.IsReturn = true;
        invoice.ReturnAgainstId = originalId;
        invoice.ReturnAgainstId.ShouldBe(originalId);
    }

    [Fact]
    public void SalesInvoice_ExchangeRate_DefaultIsOne()
    {
        var invoice = CreateInvoice();
        invoice.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void SalesInvoice_PaymentTermsTemplateId_Nullable()
    {
        var invoice = CreateInvoice();
        invoice.PaymentTermsTemplateId.ShouldBeNull();
        var templateId = Guid.NewGuid();
        invoice.PaymentTermsTemplateId = templateId;
        invoice.PaymentTermsTemplateId.ShouldBe(templateId);
    }

    [Fact]
    public void ReturnInvoice_NegativeQty_GrandTotalIsNegative()
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, -30m);
        // GrandTotal = qty * price + tax = (-5 * 100) + (-30) = -530
        invoice.GrandTotal.ShouldBeLessThan(0);
    }

    [Fact]
    public void ReturnInvoice_OutstandingAmount_IsNegative()
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, 0m);
        invoice.GrandTotal = -500m;  // Set explicitly for test
        invoice.OutstandingAmount.ShouldBe(-500m);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateReturnAsync_Throws_WhenCumulativeReturnExceedsOriginal()
    {
        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesInvoice, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo);

        var itemId = Guid.NewGuid();
        var origId = Guid.NewGuid();

        var original = new SalesInvoice(origId, Guid.NewGuid(), Guid.NewGuid(), "INV-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Widget", 10m, 100m, 0m);
        siRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Prior return for 6
        var priorReturn = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
        };
        priorReturn.AddItem(itemId, "Widget", -6m, 100m, 0m);
        priorReturn.Submit();

        // New return for 5 (total 11 > 10)
        var newReturn = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-002", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
        };
        newReturn.AddItem(itemId, "Widget", -5m, 100m, 0m);

        siRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<SalesInvoice> { original, priorReturn, newReturn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(newReturn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
        ex.Data["alreadyReturned"].ShouldBe(6m);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateReturnAsync_Succeeds_WhenPartialReturnsWithinOriginal()
    {
        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesInvoice, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo);

        var itemId = Guid.NewGuid();
        var origId = Guid.NewGuid();

        var original = new SalesInvoice(origId, Guid.NewGuid(), Guid.NewGuid(), "INV-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Widget", 10m, 100m, 0m);
        siRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Prior return for 4
        var priorReturn = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
        };
        priorReturn.AddItem(itemId, "Widget", -4m, 100m, 0m);
        priorReturn.Submit();

        // New return for 4 (total 8 <= 10)
        var newReturn = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-002", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
        };
        newReturn.AddItem(itemId, "Widget", -4m, 100m, 0m);

        siRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<SalesInvoice> { original, priorReturn, newReturn }.AsQueryable()));

        await manager.ValidateReturnAsync(newReturn);
    }

    [Fact]
    public void ReturnInvoice_AddItem_ZeroOrPositiveQty_ThrowsArgumentException()
    {
        var returnInvoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-ZERO", DateTime.UtcNow)
        {
            IsReturn = true
        };

        Should.Throw<ArgumentException>(() => returnInvoice.AddItem(Guid.NewGuid(), "Widget Zero", 0m, 100m, 0m));
        Should.Throw<ArgumentException>(() => returnInvoice.AddItem(Guid.NewGuid(), "Widget Pos", 5m, 100m, 0m));
    }

    [Fact]
    public async System.Threading.Tasks.Task SalesInvoice_ValidateReturnAsync_Throws_WhenItemNotInOriginal()
    {
        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesInvoice, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo);

        var origId = Guid.NewGuid();
        var original = new SalesInvoice(origId, Guid.NewGuid(), Guid.NewGuid(), "INV-ORIG", DateTime.UtcNow);
        original.AddItem(Guid.NewGuid(), "Item A", 10m, 100m, 0m);
        siRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        var returnInvoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnInvoice.AddItem(Guid.NewGuid(), "Item B (Foreign)", -2m, 100m, 0m);

        siRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<SalesInvoice> { original, returnInvoice }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnInvoice));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnItemNotFoundInOriginal);
    }

    [Fact]
    public async System.Threading.Tasks.Task SalesInvoice_ValidateReturnAsync_Throws_WhenSameReturnMultipleRowsExceedOriginal()
    {
        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesInvoice, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo);

        var itemId = Guid.NewGuid();
        var origId = Guid.NewGuid();
        var original = new SalesInvoice(origId, Guid.NewGuid(), Guid.NewGuid(), "INV-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Widget", 5m, 100m, 0m);
        siRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Return has two lines each returning 3 (total 6 > 5)
        var returnInvoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-MULTI", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnInvoice.AddItem(itemId, "Widget Row 1", -3m, 100m, 0m);
        returnInvoice.AddItem(itemId, "Widget Row 2", -3m, 100m, 0m);

        siRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<SalesInvoice> { original, returnInvoice }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnInvoice));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenItemNotInOriginal()
    {
        var dnRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<DeliveryNote, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var companyRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo);

        var origId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(Guid.NewGuid(), "Item Delivered", 10m, 50m, 0m);
        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        var returnDn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnDn.AddItem(Guid.NewGuid(), "Item Not Delivered", -2m, 50m, 0m);

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, returnDn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnDn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnItemNotFoundInOriginal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenSameReturnMultipleRowsExceedOriginal()
    {
        var dnRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<DeliveryNote, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var companyRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo);

        var origId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Item 1", 5m, 50m, 0m);
        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Two return rows in the same return DN: -3 and -3 (sum 6 > 5)
        var returnDn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnDn.AddItem(itemId, "Item 1 Line A", -3m, 50m, 0m);
        returnDn.AddItem(itemId, "Item 1 Line B", -3m, 50m, 0m);

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, returnDn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnDn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenBatchReturnExceedsDeliveredQty()
    {
        var dnRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<DeliveryNote, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var companyRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo);

        var origId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var batchId1 = Guid.NewGuid();
        var batchId2 = Guid.NewGuid();

        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Batched Item", 3m, 50m, 0m);
        original.Items[0].BatchId = batchId1;
        original.AddItem(itemId, "Batched Item", 2m, 50m, 0m);
        original.Items[1].BatchId = batchId2;

        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Prior return returned 3 of batch 1
        var priorReturn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET-1", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        priorReturn.AddItem(itemId, "Batched Item Return", -3m, 50m, 0m);
        priorReturn.Items[0].BatchId = batchId1;
        priorReturn.Submit();

        // New return tries to return another 2 of batch 1 (only 3 was delivered of batch 1!)
        var newReturn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET-2", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        newReturn.AddItem(itemId, "Batched Item Return 2", -2m, 50m, 0m);
        newReturn.Items[0].BatchId = batchId1;

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, priorReturn, newReturn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(newReturn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnBatchQtyExceedsDelivered);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Succeeds_WhenBatchReturnWithinDeliveredQty()
    {
        var dnRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<DeliveryNote, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var companyRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo);

        var origId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var batchId1 = Guid.NewGuid();
        var batchId2 = Guid.NewGuid();

        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Batched Item", 3m, 50m, 0m);
        original.Items[0].BatchId = batchId1;
        original.AddItem(itemId, "Batched Item", 2m, 50m, 0m);
        original.Items[1].BatchId = batchId2;

        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Return 3 of batch 1 and 2 of batch 2 (exact match)
        var returnDn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnDn.AddItem(itemId, "Return Batch 1", -3m, 50m, 0m);
        returnDn.Items[0].BatchId = batchId1;
        returnDn.AddItem(itemId, "Return Batch 2", -2m, 50m, 0m);
        returnDn.Items[1].BatchId = batchId2;

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, returnDn }.AsQueryable()));

        await manager.ValidateReturnAsync(returnDn);
    }

    [Fact]
    public async System.Threading.Tasks.Task SalesInvoice_ValidateReturnAsync_ClearsTotalAdvance()
    {
        var siRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesInvoice, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo);

        var origId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var original = new SalesInvoice(origId, Guid.NewGuid(), Guid.NewGuid(), "INV-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Item A", 10m, 100m, 0m);
        siRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        var returnInvoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
            TotalAdvance = 250m
        };
        returnInvoice.AddItem(itemId, "Item A", -2m, 100m, 0m);

        siRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<SalesInvoice> { original, returnInvoice }.AsQueryable()));

        await manager.ValidateReturnAsync(returnInvoice);
        returnInvoice.TotalAdvance.ShouldBe(0m);
    }

    [Fact]
    public async System.Threading.Tasks.Task PurchaseInvoice_ValidateReturnAsync_ClearsTotalAdvance()
    {
        var supplierRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
        var piRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>>();
        var poRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>();
        var manager = new MyERP.Purchasing.DomainServices.PurchaseInvoiceManager(supplierRepo, piRepo, poRepo);

        var origId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var original = new MyERP.Purchasing.Entities.PurchaseInvoice(origId, Guid.NewGuid(), Guid.NewGuid(), "PINV-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Item A", 10m, 100m, 0m);
        piRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        var returnInvoice = new MyERP.Purchasing.Entities.PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId,
            TotalAdvance = 150m
        };
        returnInvoice.AddItem(itemId, "Item A", -2m, 100m, 0m);

        piRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<MyERP.Purchasing.Entities.PurchaseInvoice> { original, returnInvoice }.AsQueryable()));

        await manager.ValidateReturnAsync(returnInvoice);
        returnInvoice.TotalAdvance.ShouldBe(0m);
    }
}
