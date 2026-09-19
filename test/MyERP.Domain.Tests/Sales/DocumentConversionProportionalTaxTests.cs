using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.ObjectMapping;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class DocumentConversionProportionalTaxTests
{
    private static void ConfigureLazyServiceProvider(object service)
    {
        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<IGuidGenerator>(Arg.Any<Func<IServiceProvider, object>>()).Returns(guidGen);
        lazyProvider.LazyGetService<IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(IGuidGenerator)).Returns(guidGen);

        var mapper = Substitute.For<IObjectMapper>();
        mapper.Map<SalesInvoice, SalesInvoiceDto>(Arg.Any<SalesInvoice>()).Returns(_ => new SalesInvoiceDto());
        mapper.Map<DeliveryNote, DeliveryNoteDto>(Arg.Any<DeliveryNote>()).Returns(_ => new DeliveryNoteDto());
        mapper.Map<PurchaseReceipt, PurchaseReceiptDto>(Arg.Any<PurchaseReceipt>()).Returns(_ => new PurchaseReceiptDto());
        mapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(Arg.Any<PurchaseInvoice>()).Returns(_ => new PurchaseInvoiceDto());
        lazyProvider.LazyGetService<IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>()).Returns(mapper);
        lazyProvider.LazyGetService<IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetRequiredService<IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetService(typeof(IObjectMapper)).Returns(mapper);
        lazyProvider.LazyGetRequiredService(typeof(IObjectMapper)).Returns(mapper);

        if (service is ApplicationService appService)
        {
            appService.LazyServiceProvider = lazyProvider;
        }
        else if (service is DomainService domainService)
        {
            domainService.LazyServiceProvider = lazyProvider;
        }
    }

    private static DocumentActivityLogService CreateActivityLogService()
    {
        var activityLog = new DocumentActivityLogService(
            Substitute.For<IRepository<DocumentActivityLog, Guid>>(),
            Substitute.For<Volo.Abp.Users.ICurrentUser>());
        ConfigureLazyServiceProvider(activityLog);
        return activityLog;
    }

    [Fact]
    public async Task ConvertPurchaseOrderToReceipt_ProRatesLineTaxOnPartialReceipt()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0001", DateTime.UtcNow);
        po.AddItem(itemId, "Widget", quantity: 10m, unitPrice: 100m, taxAmount: 60m, uom: "Unit", warehouseId: warehouseId);
        po.Submit();

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        poRepo.GetAsync(po.Id).Returns(Task.FromResult(po));

        // 6 units already mapped in a draft receipt -> only 4 pending
        var draftPr = new PurchaseReceipt(Guid.NewGuid(), companyId, supplierId, warehouseId, "PR-DRAFT", DateTime.UtcNow);
        draftPr.AddItem(itemId, "Widget", quantity: 6m, unitPrice: 100m, taxAmount: 36m, uom: "Unit", purchaseOrderItemId: po.Items[0].Id);

        var prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        var prList = new List<PurchaseReceipt> { draftPr };
        prRepo.GetQueryableAsync().Returns(Task.FromResult(prList.AsQueryable()));

        PurchaseReceipt? savedReceipt = null;
        await prRepo.InsertAsync(Arg.Do<PurchaseReceipt>(r => savedReceipt = r), Arg.Any<bool>());

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("PurchaseReceipt", companyId).Returns(Task.FromResult("PR-0002"));

        var activityLog = CreateActivityLogService();

        var service = new PurchaseConversionAppService(
            poRepo,
            prRepo,
            Substitute.For<IRepository<PurchaseInvoice, Guid>>(),
            Substitute.For<IRepository<MaterialRequest, Guid>>(),
            Substitute.For<IRepository<RequestForQuotation, Guid>>(),
            Substitute.For<IRepository<SupplierQuotation, Guid>>(),
            Substitute.For<IRepository<Supplier, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            numGen,
            activityLog);
        ConfigureLazyServiceProvider(service);

        await service.ConvertPurchaseOrderToReceiptAsync(po.Id);

        savedReceipt.ShouldNotBeNull();
        savedReceipt.Items.Count.ShouldBe(1);
        savedReceipt.Items[0].Quantity.ShouldBe(4m);
        // Tax is pro-rated: 60 * (4 / 10) = 24
        savedReceipt.Items[0].TaxAmount.ShouldBe(24m);
    }

    [Fact]
    public async Task ConvertPurchaseOrderToInvoice_ProRatesLineTaxOnPartialInvoice()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0002", DateTime.UtcNow);
        po.AddItem(itemId, "Component", quantity: 10m, unitPrice: 50m, taxAmount: 50m, uom: "Unit");
        po.Submit();

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        poRepo.GetAsync(po.Id).Returns(Task.FromResult(po));

        // 5 units already in draft invoice -> 5 pending
        var draftPi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "PI-DRAFT", DateTime.UtcNow);
        draftPi.AddItem(itemId, "Component", quantity: 5m, unitPrice: 50m, taxAmount: 25m, uom: "Unit");
        draftPi.Items[0].PurchaseOrderItemId = po.Items[0].Id;

        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var piList = new List<PurchaseInvoice> { draftPi };
        piRepo.GetQueryableAsync().Returns(Task.FromResult(piList.AsQueryable()));

        PurchaseInvoice? savedInvoice = null;
        await piRepo.InsertAsync(Arg.Do<PurchaseInvoice>(i => savedInvoice = i), Arg.Any<bool>());

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("PurchaseInvoice", companyId).Returns(Task.FromResult("PI-0002"));

        var activityLog = CreateActivityLogService();

        var service = new PurchaseConversionAppService(
            poRepo,
            Substitute.For<IRepository<PurchaseReceipt, Guid>>(),
            piRepo,
            Substitute.For<IRepository<MaterialRequest, Guid>>(),
            Substitute.For<IRepository<RequestForQuotation, Guid>>(),
            Substitute.For<IRepository<SupplierQuotation, Guid>>(),
            Substitute.For<IRepository<Supplier, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            numGen,
            activityLog);
        ConfigureLazyServiceProvider(service);

        await service.ConvertPurchaseOrderToInvoiceAsync(po.Id);

        savedInvoice.ShouldNotBeNull();
        savedInvoice.Items.Count.ShouldBe(1);
        savedInvoice.Items[0].Quantity.ShouldBe(5m);
        // Tax is pro-rated: 50 * (5 / 10) = 25
        savedInvoice.Items[0].TaxAmount.ShouldBe(25m);
    }

    [Fact]
    public async Task ConvertSalesOrderToDeliveryNote_ProRatesLineTaxOnSelectedPartialItems()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0001", DateTime.UtcNow);
        so.AddItem(itemId, "Finished Good", quantity: 10m, unitPrice: 200m, taxAmount: 120m, uom: "Unit");
        so.Items.Last().WarehouseId = warehouseId;
        so.Submit();

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetAsync(so.Id).Returns(Task.FromResult(so));

        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        DeliveryNote? savedDn = null;
        await dnRepo.InsertAsync(Arg.Do<DeliveryNote>(dn => savedDn = dn), Arg.Any<bool>());

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("DeliveryNote", companyId).Returns(Task.FromResult("DN-0001"));

        var activityLog = CreateActivityLogService();

        var service = new DocumentConversionAppService(
            Substitute.For<IRepository<Quotation, Guid>>(),
            soRepo,
            dnRepo,
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<Customer, Guid>>(),
            numGen,
            activityLog);
        ConfigureLazyServiceProvider(service);

        var selected = new List<PartialDeliveryItemDto>
        {
            new PartialDeliveryItemDto { SalesOrderItemId = so.Items[0].Id, Quantity = 3m, WarehouseId = warehouseId }
        };

        await service.ConvertSalesOrderToDeliveryNoteAsync(so.Id, selected);

        savedDn.ShouldNotBeNull();
        savedDn.Items.Count.ShouldBe(1);
        savedDn.Items[0].Quantity.ShouldBe(3m);
        // Tax is pro-rated: 120 * (3 / 10) = 36
        savedDn.Items[0].TaxAmount.ShouldBe(36m);
    }

    [Fact]
    public async Task ConvertSalesOrderToInvoice_ProRatesLineTaxOnPartialBilling()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0002", DateTime.UtcNow);
        so.AddItem(itemId, "Service Item", quantity: 10m, unitPrice: 100m, taxAmount: 80m, uom: "Unit");
        so.Submit();

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetAsync(so.Id).Returns(Task.FromResult(so));

        // 6 units already in draft invoice -> 4 pending
        var draftSi = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-DRAFT", DateTime.UtcNow);
        draftSi.AddItem(itemId, "Service Item", quantity: 6m, unitPrice: 100m, taxAmount: 48m, uom: "Unit");
        draftSi.Items[0].SalesOrderItemId = so.Items[0].Id;

        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var siList = new List<SalesInvoice> { draftSi };
        siRepo.GetQueryableAsync().Returns(Task.FromResult(siList.AsQueryable()));

        SalesInvoice? savedInvoice = null;
        await siRepo.InsertAsync(Arg.Do<SalesInvoice>(si => savedInvoice = si), Arg.Any<bool>());

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("SalesInvoice", companyId).Returns(Task.FromResult("SI-0002"));

        var activityLog = CreateActivityLogService();

        var service = new DocumentConversionAppService(
            Substitute.For<IRepository<Quotation, Guid>>(),
            soRepo,
            Substitute.For<IRepository<DeliveryNote, Guid>>(),
            siRepo,
            Substitute.For<IRepository<Customer, Guid>>(),
            numGen,
            activityLog);
        ConfigureLazyServiceProvider(service);

        await service.ConvertSalesOrderToSalesInvoiceAsync(so.Id);

        savedInvoice.ShouldNotBeNull();
        savedInvoice.Items.Count.ShouldBe(1);
        savedInvoice.Items[0].Quantity.ShouldBe(4m);
        // Tax is pro-rated: 80 * (4 / 10) = 32
        savedInvoice.Items[0].TaxAmount.ShouldBe(32m);
    }

    [Fact]
    public async Task ConvertDeliveryNoteToInvoice_ProRatesLineTaxOnPartialInvoicing()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var dn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, warehouseId, "DN-0002", DateTime.UtcNow);
        dn.AddItem(itemId, "Product", quantity: 10m, unitPrice: 150m, taxAmount: 90m, uom: "Unit");
        dn.Submit();

        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        dnRepo.GetAsync(dn.Id).Returns(Task.FromResult(dn));

        // 7 units already billed in draft invoice -> 3 pending
        var draftSi = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-DRAFT2", DateTime.UtcNow);
        draftSi.AddItem(itemId, "Product", quantity: 7m, unitPrice: 150m, taxAmount: 63m, uom: "Unit");
        draftSi.Items[0].DeliveryNoteItemId = dn.Items[0].Id;

        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var siList = new List<SalesInvoice> { draftSi };
        siRepo.GetQueryableAsync().Returns(Task.FromResult(siList.AsQueryable()));

        SalesInvoice? savedInvoice = null;
        await siRepo.InsertAsync(Arg.Do<SalesInvoice>(si => savedInvoice = si), Arg.Any<bool>());

        var numGen = Substitute.For<IDocumentNumberGenerator>();
        numGen.GenerateAsync("SalesInvoice", companyId).Returns(Task.FromResult("SI-0003"));

        var activityLog = CreateActivityLogService();

        var service = new DocumentConversionAppService(
            Substitute.For<IRepository<Quotation, Guid>>(),
            Substitute.For<IRepository<SalesOrder, Guid>>(),
            dnRepo,
            siRepo,
            Substitute.For<IRepository<Customer, Guid>>(),
            numGen,
            activityLog);
        ConfigureLazyServiceProvider(service);

        await service.ConvertDeliveryNoteToSalesInvoiceAsync(dn.Id);

        savedInvoice.ShouldNotBeNull();
        savedInvoice.Items.Count.ShouldBe(1);
        savedInvoice.Items[0].Quantity.ShouldBe(3m);
        // Tax is pro-rated: 90 * (3 / 10) = 27
        savedInvoice.Items[0].TaxAmount.ShouldBe(27m);
    }
}
