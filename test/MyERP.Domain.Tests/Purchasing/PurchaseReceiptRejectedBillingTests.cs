using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

public class PurchaseReceiptRejectedBillingTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    [Fact]
    public void FullyRejectedReceipt_BillableQty_EqualsRejectedQty()
    {
        // Per ERPNext PR #58885 / commit 3761eb8cbe:
        // When receipt is fully rejected (qty=0, rejected_qty=10), billable base must include rejected qty
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Raw Material", 0m, 9.5m, 0m, rejectedQty: 10m);
        var item = pr.Items[0];

        item.BillableQty.ShouldBe(10m);
        item.PendingBillingQty.ShouldBe(10m);
    }

    [Fact]
    public void FullyRejectedReceipt_BilledQtyMatchesRejectedQty_PerBilledIs100PercentAndCompleted()
    {
        // Per ERPNext PR #58885: Billing rejected qty must not push per_billed above 100, and marks it Completed
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Raw Material", 0m, 9.5m, 0m, rejectedQty: 10m);
        var item = pr.Items[0];
        pr.Submit();

        item.BilledQty = 10m;

        pr.PerBilled.ShouldBe(100m);
        pr.BillingStatus.ShouldBe("Completed");
    }

    [Fact]
    public void FullyRejectedReceipt_PartialBilled_PerBilledIsProportional()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Raw Material", 0m, 9.5m, 0m, rejectedQty: 10m);
        var item = pr.Items[0];
        pr.Submit();

        item.BilledQty = 5m;

        pr.PerBilled.ShouldBe(50m);
        pr.BillingStatus.ShouldBe("Partially Billed");
    }

    [Fact]
    public async Task ConvertPurchaseReceiptToInvoice_FullyRejectedReceipt_MapsPendingRejectedQty()
    {
        // Fully rejected PR should be convertible to PI using PendingBillingQty
        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();

        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-REJ", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Raw Material", 0m, 9.5m, 0m, rejectedQty: 10m);
        pr.Submit();

        prRepo.GetAsync(pr.Id).Returns(Task.FromResult(pr));
        var emptyPiList = new List<PurchaseInvoice>().AsQueryable();
        piRepo.GetQueryableAsync().Returns(Task.FromResult(emptyPiList));

        var activityLog = new DocumentActivityLogService(
            Substitute.For<IRepository<DocumentActivityLog, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>());

        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        numberGen.GenerateAsync(Arg.Any<string>(), Arg.Any<Guid>()).Returns(Task.FromResult("PI-001"));

        var appService = new PurchaseConversionAppService(
            Substitute.For<IRepository<PurchaseOrder, Guid>>(),
            prRepo,
            piRepo,
            Substitute.For<IRepository<MaterialRequest, Guid>>(),
            Substitute.For<IRepository<RequestForQuotation, Guid>>(),
            Substitute.For<IRepository<SupplierQuotation, Guid>>(),
            Substitute.For<IRepository<Supplier, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            numberGen,
            activityLog);

        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);

        var mapper = Substitute.For<Volo.Abp.ObjectMapping.IObjectMapper>();
        mapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(Arg.Any<PurchaseInvoice>()).Returns(call =>
        {
            var inv = call.Arg<PurchaseInvoice>();
            return new PurchaseInvoiceDto
            {
                Id = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                Items = inv.Items.Select(i => new PurchaseInvoiceItemDto
                {
                    ItemId = i.ItemId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };
        });
        PurchaseInvoice? capturedInvoice = null;
        await piRepo.InsertAsync(Arg.Do<PurchaseInvoice>(inv => capturedInvoice = inv), autoSave: true);

        lazyProvider.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>())
            .Returns(mapper);
        lazyProvider.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetRequiredService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(mapper);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(mapper);

        appService.LazyServiceProvider = lazyProvider;
        activityLog.LazyServiceProvider = lazyProvider;

        await appService.ConvertPurchaseReceiptToInvoiceAsync(pr.Id);

        capturedInvoice.ShouldNotBeNull();
        capturedInvoice.Items.Count.ShouldBe(1);
        capturedInvoice.Items[0].Quantity.ShouldBe(10m);
        capturedInvoice.Items[0].UnitPrice.ShouldBe(9.5m);
    }

    [Fact]
    public async Task ConvertPurchaseReceiptToInvoice_FullyBilled_ThrowsDocumentAlreadyConverted()
    {
        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();

        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Raw Material", 0m, 9.5m, 0m, rejectedQty: 10m);
        var item = pr.Items[0];
        item.BilledQty = 10m;
        pr.Submit();

        prRepo.GetAsync(pr.Id).Returns(Task.FromResult(pr));

        var activityLog = new DocumentActivityLogService(
            Substitute.For<IRepository<DocumentActivityLog, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>());

        var appService = new PurchaseConversionAppService(
            Substitute.For<IRepository<PurchaseOrder, Guid>>(),
            prRepo,
            piRepo,
            Substitute.For<IRepository<MaterialRequest, Guid>>(),
            Substitute.For<IRepository<RequestForQuotation, Guid>>(),
            Substitute.For<IRepository<SupplierQuotation, Guid>>(),
            Substitute.For<IRepository<Supplier, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            activityLog);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await appService.ConvertPurchaseReceiptToInvoiceAsync(pr.Id));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentAlreadyConverted);
        (ex.Data["reason"]?.ToString() ?? string.Empty).ShouldContain("already fully billed");
    }
}
