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
}
