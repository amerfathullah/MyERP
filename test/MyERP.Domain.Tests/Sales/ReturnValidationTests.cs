using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
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

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenDuplicateSerialInSameReturnDoc()
    {
        // Per ERPNext PR #64590 (commit 9403e3df8d):
        // Return rows that refer to the same original row must not pick the same serial no
        var dnRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<DeliveryNote, Guid>>();
        var soRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SalesOrder, Guid>>();
        var companyRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var manager = new MyERP.Sales.DomainServices.DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo);

        var origId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var serialId1 = Guid.NewGuid();

        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Serialized Item", 2m, 100m, 0m);
        original.Items[0].SerialNoId = serialId1;
        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        var returnDn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnDn.AddItem(itemId, "Return Row 1", -1m, 100m, 0m);
        returnDn.Items[0].SerialNoId = serialId1;
        returnDn.AddItem(itemId, "Return Row 2", -1m, 100m, 0m);
        returnDn.Items[1].SerialNoId = serialId1; // Duplicate serial!

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, returnDn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnDn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnDuplicateSerialInDocument);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenSerialNotFoundInOriginal()
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
        var originalSerialId = Guid.NewGuid();
        var unknownSerialId = Guid.NewGuid();

        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Serialized Item", 1m, 100m, 0m);
        original.Items[0].SerialNoId = originalSerialId;
        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        var returnDn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        returnDn.AddItem(itemId, "Return Row", -1m, 100m, 0m);
        returnDn.Items[0].SerialNoId = unknownSerialId; // not in original

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, returnDn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnDn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnSerialNotFoundInOriginal);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenSerialAlreadyReturned()
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
        var serialId = Guid.NewGuid();

        var original = new DeliveryNote(origId, companyId, customerId, Guid.NewGuid(), "DN-ORIG", DateTime.UtcNow);
        original.AddItem(itemId, "Serialized Item", 2m, 100m, 0m);
        original.Items[0].SerialNoId = serialId;
        dnRepo.GetAsync(origId).Returns(System.Threading.Tasks.Task.FromResult(original));

        // Prior submitted return already returned serialId
        var priorReturn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET-1", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        priorReturn.AddItem(itemId, "Prior Return", -1m, 100m, 0m);
        priorReturn.Items[0].SerialNoId = serialId;
        priorReturn.Submit();

        var newReturn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET-2", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = origId
        };
        newReturn.AddItem(itemId, "New Return", -1m, 100m, 0m);
        newReturn.Items[0].SerialNoId = serialId;

        dnRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<DeliveryNote> { original, priorReturn, newReturn }.AsQueryable()));

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(newReturn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnSerialAlreadyReturned);
    }

    [Fact]
    public async System.Threading.Tasks.Task SalesInvoice_ValidateReturnAsync_AllowsStandaloneReturn_WhenCompanyAllowsAndExternalRefGiven()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var companyId = Guid.NewGuid();
        var company = new Company(companyId, "Test Company")
        {
            AllowCreditNoteWithoutOriginalInvoice = true
        };
        companyRepo.FindAsync(companyId).Returns(company);

        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo, companyRepo);

        var returnInvoice = new SalesInvoice(Guid.NewGuid(), companyId, Guid.NewGuid(), "CN-EXT-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = null,
            OriginalInvoiceNumber = "LEGACY-INV-101",
            TotalAdvance = 500m
        };
        returnInvoice.AddItem(Guid.NewGuid(), "Return Item", -1m, 100m, 0m);

        await manager.ValidateReturnAsync(returnInvoice);
        returnInvoice.TotalAdvance.ShouldBe(0m);
    }

    [Fact]
    public async System.Threading.Tasks.Task SalesInvoice_ValidateReturnAsync_Throws_WhenStandaloneReturnAndCompanyDisallows()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var companyId = Guid.NewGuid();
        var company = new Company(companyId, "Test Company")
        {
            AllowCreditNoteWithoutOriginalInvoice = false
        };
        companyRepo.FindAsync(companyId).Returns(company);

        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo, companyRepo);

        var returnInvoice = new SalesInvoice(Guid.NewGuid(), companyId, Guid.NewGuid(), "CN-EXT-002", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = null,
            OriginalInvoiceNumber = "LEGACY-INV-102"
        };
        returnInvoice.AddItem(Guid.NewGuid(), "Return Item", -1m, 100m, 0m);

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnInvoice));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnMustReferenceOriginal);
    }

    [Fact]
    public async System.Threading.Tasks.Task SalesInvoice_ValidateReturnAsync_Throws_WhenStandaloneReturnSerialBelongsToDifferentCustomer()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var serialRepo = Substitute.For<IRepository<SerialNo, Guid>>();

        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var serialId = Guid.NewGuid();

        var company = new Company(companyId, "Test Company")
        {
            AllowCreditNoteWithoutOriginalInvoice = true
        };
        companyRepo.FindAsync(companyId).Returns(company);

        var serialNo = new SerialNo(serialId, itemId, "SN-OTHER-01", companyId)
        {
            CustomerId = otherCustomerId
        };

        serialRepo.GetQueryableAsync().Returns(new List<SerialNo> { serialNo }.AsQueryable());

        var serialValidationService = new SerialNoValidationService(serialRepo);
        var manager = new MyERP.Sales.DomainServices.SalesInvoiceManager(siRepo, soRepo, itemRepo, companyRepo, serialValidationService);

        var returnInvoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "CN-EXT-003", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = null,
            OriginalInvoiceNumber = "LEGACY-INV-103"
        };
        returnInvoice.AddItem(itemId, "Return Item", -1m, 100m, 0m);
        returnInvoice.Items[0].SerialNoId = serialId;

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnInvoice));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnSerialCustomerMismatch);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeliveryNote_ValidateReturnAsync_Throws_WhenStandaloneReturnSerialBelongsToDifferentCustomer()
    {
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var serialRepo = Substitute.For<IRepository<SerialNo, Guid>>();

        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var serialId = Guid.NewGuid();

        var company = new Company(companyId, "Test Company")
        {
            AllowCreditNoteWithoutOriginalInvoice = true
        };
        companyRepo.FindAsync(companyId).Returns(company);

        var serialNo = new SerialNo(serialId, itemId, "SN-OTHER-DN-01", companyId)
        {
            CustomerId = otherCustomerId
        };

        serialRepo.GetQueryableAsync().Returns(new List<SerialNo> { serialNo }.AsQueryable());

        var serialValidationService = new SerialNoValidationService(serialRepo);
        var manager = new MyERP.Sales.DomainServices.DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo, serialNoValidationService: serialValidationService);

        var returnDn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-RET-EXT-01", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = null
        };
        returnDn.AddItem(itemId, "Return Item", -1m, 100m, 0m);
        returnDn.Items[0].SerialNoId = serialId;

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnDn));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnSerialCustomerMismatch);
    }

    [Fact]
    public async System.Threading.Tasks.Task PurchaseInvoice_ValidateReturnAsync_AllowsStandaloneReturn_WhenCompanyAllowsAndExternalRefGiven()
    {
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var companyId = Guid.NewGuid();
        var company = new Company(companyId, "Test Company")
        {
            AllowCreditNoteWithoutOriginalInvoice = true
        };
        companyRepo.FindAsync(companyId).Returns(company);

        var manager = new PurchaseInvoiceManager(suppRepo, piRepo, poRepo, companyRepo);

        var returnPi = new PurchaseInvoice(Guid.NewGuid(), companyId, Guid.NewGuid(), "DN-SUPP-EXT-001", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = null,
            OriginalInvoiceNumber = "EXT-SUPP-INV-101",
            TotalAdvance = 400m
        };
        returnPi.AddItem(Guid.NewGuid(), "Return Item", -1m, 50m, 0m);

        await manager.ValidateReturnAsync(returnPi);
        returnPi.TotalAdvance.ShouldBe(0m);
    }

    [Fact]
    public async System.Threading.Tasks.Task PurchaseInvoice_ValidateReturnAsync_Throws_WhenStandaloneReturnAndCompanyDisallows()
    {
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var companyId = Guid.NewGuid();
        var company = new Company(companyId, "Test Company")
        {
            AllowCreditNoteWithoutOriginalInvoice = false
        };
        companyRepo.FindAsync(companyId).Returns(company);

        var manager = new PurchaseInvoiceManager(suppRepo, piRepo, poRepo, companyRepo);

        var returnPi = new PurchaseInvoice(Guid.NewGuid(), companyId, Guid.NewGuid(), "DN-SUPP-EXT-002", DateTime.UtcNow)
        {
            IsReturn = true,
            ReturnAgainstId = null,
            OriginalInvoiceNumber = "EXT-SUPP-INV-102"
        };
        returnPi.AddItem(Guid.NewGuid(), "Return Item", -1m, 50m, 0m);

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => manager.ValidateReturnAsync(returnPi));
        ex.Code.ShouldBe("MyERP:08002");
    }
}
