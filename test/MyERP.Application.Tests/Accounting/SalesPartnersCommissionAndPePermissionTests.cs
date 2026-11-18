using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class SalesPartnersCommissionAndPePermissionTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportingAppService _reportingAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesPartner, Guid> _partnerRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    protected SalesPartnersCommissionAndPePermissionTests()
    {
        _reportingAppService = GetRequiredService<IReportingAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _partnerRepository = GetRequiredService<IRepository<SalesPartner, Guid>>();
        _salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
        _salesOrderRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
    }

    [Fact]
    public async Task SalesPartnersCommission_Should_Aggregate_And_Reverse_Credit_Notes()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "SPC Co") { CurrencyCode = "MYR" }, autoSave: true);
        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Alpha Buyer"), autoSave: true);
        var partner = await _partnerRepository.InsertAsync(
            new SalesPartner(Guid.NewGuid(), "Partner Alpha", PartnerType.Agent, 10m), autoSave: true);
        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-SPC", "SPC Service", ItemType.Service), autoSave: true);

        var si1 = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-SPC-1", new DateTime(2026, 6, 1))
        {
            SalesPartnerId = partner.Id,
            CommissionRate = 10m,
            TotalCommission = 100m,
        };
        si1.AddItem(item.Id, "Service", 1m, 1000m, 0m);
        si1.BaseNetTotal = 1000m;
        si1.Submit();
        si1.Post();
        await _salesInvoiceRepository.InsertAsync(si1, autoSave: true);

        var si2 = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-SPC-2", new DateTime(2026, 6, 10))
        {
            SalesPartnerId = partner.Id,
            CommissionRate = 10m,
            TotalCommission = 40m,
        };
        si2.AddItem(item.Id, "Service", 1m, 400m, 0m);
        si2.BaseNetTotal = 400m;
        si2.Submit();
        si2.Post();
        await _salesInvoiceRepository.InsertAsync(si2, autoSave: true);

        // Credit note reversing si1
        var cn = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "CN-SPC-1", new DateTime(2026, 6, 15))
        {
            SalesPartnerId = partner.Id,
            CommissionRate = 10m,
            IsReturn = true,
            ReturnAgainstId = si1.Id,
            TotalCommission = -100m,
        };
        cn.AddItem(item.Id, "Return Service", -1m, 1000m, 0m);
        cn.BaseNetTotal = -1000m;
        cn.Submit();
        cn.Post();
        await _salesInvoiceRepository.InsertAsync(cn, autoSave: true);

        // Out-of-range historical invoice excluded by FromDate filter
        var siOld = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-SPC-OLD", new DateTime(2026, 4, 1))
        {
            SalesPartnerId = partner.Id,
            CommissionRate = 10m,
            TotalCommission = 30m,
        };
        siOld.AddItem(item.Id, "Old Service", 1m, 300m, 0m);
        siOld.BaseNetTotal = 300m;
        siOld.Submit();
        siOld.Post();
        await _salesInvoiceRepository.InsertAsync(siOld, autoSave: true);

        var filtered = await _reportingAppService.GetSalesPartnersCommissionAsync(new SalesPartnersCommissionRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
        });

        var row = filtered.Rows.Single(r => r.SalesPartnerId == partner.Id);
        row.InvoicedAmount.ShouldBe(400m);
        row.AmountEligibleForCommission.ShouldBe(400m);
        row.TotalCommission.ShouldBe(40m);
        row.AverageCommissionRate.ShouldBe(10m);
    }

    [Fact]
    public async Task PaymentEntry_GetPartyOutstanding_Should_Filter_Unpermitted_Voucher_Types()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "PE Perm Co") { CurrencyCode = "MYR" }, autoSave: true);
        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Perm Buyer"), autoSave: true);
        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "ITEM-PERM", "Perm Item", ItemType.Service), autoSave: true);

        var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-PERM-1", new DateTime(2026, 6, 1));
        si.AddItem(item.Id, "Product", 1m, 500m, 0m);
        si.Submit();
        si.Post();
        await _salesInvoiceRepository.InsertAsync(si, autoSave: true);

        var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PERM-1", new DateTime(2026, 6, 1));
        so.AddItem(item.Id, "Product", 1m, 300m, 0m);
        so.Submit();
        await _salesOrderRepository.InsertAsync(so, autoSave: true);

        var authMock = Substitute.For<IAuthorizationService>();
        authMock.AuthorizeAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<object?>(),
                MyERPPermissions.SalesInvoices.Default)
            .Returns(AuthorizationResult.Success());
        authMock.AuthorizeAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<object?>(),
                MyERPPermissions.SalesOrders.Default)
            .Returns(AuthorizationResult.Failed());

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<IAuthorizationService>().Returns(authMock);
        lazyProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>().Returns(_salesInvoiceRepository);
        lazyProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>().Returns(_salesOrderRepository);

        var peAppService = new PaymentEntryAppService(
            GetRequiredService<IRepository<PaymentEntry, Guid>>(),
            _salesInvoiceRepository,
            GetRequiredService<IRepository<PurchaseInvoice, Guid>>(),
            GetRequiredService<IRepository<PaymentScheduleEntry, Guid>>(),
            GetRequiredService<IRepository<PaymentLedgerEntry, Guid>>(),
            GetRequiredService<IDocumentNumberGenerator>(),
            GetRequiredService<DocumentPostingOrchestrator>(),
            GetRequiredService<IRepository<DocumentActivityLog, Guid>>())
        {
            LazyServiceProvider = lazyProvider,
        };

        var result = await peAppService.GetPartyOutstandingAsync("Customer", customer.Id, company.Id);

        result.Invoices.Select(i => i.InvoiceId).ShouldContain(si.Id);
        result.Orders.ShouldBeEmpty();
    }
}
