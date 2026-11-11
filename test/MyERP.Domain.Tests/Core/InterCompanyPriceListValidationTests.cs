using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Tests.Core;

/// <summary>
/// Inter-company price list guard.
/// Per ERPNext accounts/doctype/sales_invoice/mapper.py validate_inter_company_transaction():
/// selected price list must have both buying=1 and selling=1.
/// </summary>
public class InterCompanyPriceListValidationTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<PurchaseOrder, Guid> _poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<PriceList, Guid> _priceListRepo = Substitute.For<IRepository<PriceList, Guid>>();

    private InterCompanyTransactionService CreateService() =>
        new(_siRepo, _piRepo, _poRepo, _companyRepo, _customerRepo, _supplierRepo, _priceListRepo);

    private (SalesInvoice Si, Guid TargetCompanyId) SeedSalesInvoice(PriceList priceList)
    {
        var source = new Company(Guid.NewGuid(), "Source Co");
        var target = new Company(Guid.NewGuid(), "Target Co");
        _companyRepo.GetAsync(source.Id).Returns(Task.FromResult(source));
        _companyRepo.GetAsync(target.Id).Returns(Task.FromResult(target));

        var customer = new Customer(Guid.NewGuid(), source.Id, "Target Co (Customer)") { RepresentsCompanyId = target.Id };
        _customerRepo.GetAsync(customer.Id).Returns(Task.FromResult(customer));

        var supplier = new Supplier(Guid.NewGuid(), target.Id, "Source Co (Supplier)") { RepresentsCompanyId = source.Id };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        _priceListRepo.FindAsync(priceList.Id).Returns(Task.FromResult<PriceList?>(priceList));

        var si = new SalesInvoice(Guid.NewGuid(), source.Id, customer.Id, "SI-IC-001", DateTime.UtcNow)
        {
            PriceListId = priceList.Id
        };
        si.AddItem(Guid.NewGuid(), "Widget", 2m, 50m, 0m);
        _siRepo.GetAsync(si.Id).Returns(Task.FromResult(si));

        return (si, target.Id);
    }

    [Fact]
    public async Task CreatePurchaseInvoice_Throws_WhenPriceListIsSellingOnly()
    {
        var pl = new PriceList(Guid.NewGuid(), "Standard Selling", "MYR", isSelling: true, isBuying: false);
        var (si, targetId) = SeedSalesInvoice(pl);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            CreateService().CreatePurchaseInvoiceFromSalesInvoiceAsync(si.Id, targetId));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InterCompanyPriceListMustBeBuyingAndSelling);
        await _piRepo.DidNotReceiveWithAnyArgs().InsertAsync(default!, default, default);
    }

    [Fact]
    public async Task CreatePurchaseInvoice_Succeeds_AndCarriesPriceList_WhenBuyingAndSelling()
    {
        var pl = new PriceList(Guid.NewGuid(), "Inter-Company", "MYR", isSelling: true, isBuying: true);
        var (si, targetId) = SeedSalesInvoice(pl);

        await CreateService().CreatePurchaseInvoiceFromSalesInvoiceAsync(si.Id, targetId);

        await _piRepo.Received(1).InsertAsync(
            Arg.Is<PurchaseInvoice>(pi => pi.PriceListId == pl.Id && pi.InterCompanyInvoiceId == si.Id),
            true, default);
    }

    [Fact]
    public async Task CreateSalesInvoice_Throws_WhenPriceListIsBuyingOnly()
    {
        var source = new Company(Guid.NewGuid(), "Source Co");
        var target = new Company(Guid.NewGuid(), "Target Co");
        _companyRepo.GetAsync(source.Id).Returns(Task.FromResult(source));
        _companyRepo.GetAsync(target.Id).Returns(Task.FromResult(target));

        var supplier = new Supplier(Guid.NewGuid(), source.Id, "Target Co (Supplier)") { RepresentsCompanyId = target.Id };
        _supplierRepo.GetAsync(supplier.Id).Returns(Task.FromResult(supplier));

        var customer = new Customer(Guid.NewGuid(), target.Id, "Source Co (Customer)") { RepresentsCompanyId = source.Id };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var pl = new PriceList(Guid.NewGuid(), "Standard Buying", "MYR", isSelling: false, isBuying: true);
        _priceListRepo.FindAsync(pl.Id).Returns(Task.FromResult<PriceList?>(pl));

        var pi = new PurchaseInvoice(Guid.NewGuid(), source.Id, supplier.Id, "PI-IC-001", DateTime.UtcNow)
        {
            PriceListId = pl.Id
        };
        _piRepo.GetAsync(pi.Id).Returns(Task.FromResult(pi));

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            CreateService().CreateSalesInvoiceFromPurchaseInvoiceAsync(pi.Id, target.Id));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InterCompanyPriceListMustBeBuyingAndSelling);
    }
}
