using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax;
using MyERP.Tax.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Application.Tests.Accounting;

/// <summary>
/// Verifies core Accounting, Tax, Sales, and Purchasing reports include intra-day
/// posted transactions up to 23:59:59.9999999 on ToDate and exclude next-day entries.
/// </summary>
public class FinancialReportsToDateCutoffTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly DateTime _fromDate = new(2026, 6, 1);
    private readonly DateTime _toDate = new(2026, 6, 30);

    private static SalesInvoice CreatePostedSalesInvoice(Guid companyId, Guid customerId, string number, DateTime issueDate, decimal qty, decimal unitPrice, decimal taxAmount = 0m)
    {
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, number, issueDate);
        si.AddItem(Guid.NewGuid(), "Widget", qty, unitPrice, taxAmount);
        si.Submit();
        si.Post();
        return si;
    }

    private static PurchaseInvoice CreatePostedPurchaseInvoice(Guid companyId, Guid supplierId, string number, DateTime issueDate, decimal qty, decimal unitPrice, decimal taxAmount = 0m)
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, number, issueDate);
        pi.AddItem(Guid.NewGuid(), "Part", qty, unitPrice, taxAmount);
        pi.Submit();
        pi.Post();
        return pi;
    }

    [Fact]
    public async Task GeneralLedger_GetReport_IncludesIntraDayJournalsOnToDate()
    {
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var accRepo = Substitute.For<IRepository<Account, Guid>>();

        var debitAccId = Guid.NewGuid();
        var creditAccId = Guid.NewGuid();
        var fyId = Guid.NewGuid();
        var acc1 = new Account(debitAccId, _companyId, "1000", "Cash", AccountType.Asset);
        var acc2 = new Account(creditAccId, _companyId, "4000", "Revenue", AccountType.Revenue);
        accRepo.GetQueryableAsync().Returns(new List<Account> { acc1, acc2 }.AsQueryable());

        var jeSameDayEvening = new JournalEntry(Guid.NewGuid(), _companyId, fyId, _toDate.AddHours(23).AddMinutes(45));
        jeSameDayEvening.AddLine(debitAccId, 250m, isDebit: true);
        jeSameDayEvening.AddLine(creditAccId, 250m, isDebit: false);
        jeSameDayEvening.Post();

        var jeNextDay = new JournalEntry(Guid.NewGuid(), _companyId, fyId, _toDate.AddDays(1));
        jeNextDay.AddLine(debitAccId, 500m, isDebit: true);
        jeNextDay.AddLine(creditAccId, 500m, isDebit: false);
        jeNextDay.Post();

        jeRepo.GetQueryableAsync().Returns(new List<JournalEntry> { jeSameDayEvening, jeNextDay }.AsQueryable());

        var svc = new GeneralLedgerAppService(jeRepo, accRepo);

        var report = await svc.GetReportAsync(new GeneralLedgerFilterDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });

        report.TotalDebit.ShouldBe(250m);
        report.TotalCredit.ShouldBe(250m);
    }

    [Fact]
    public async Task StatementOfAccounts_IncludesIntraDayInvoicesOnToDate()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var supRepo = Substitute.For<IRepository<Supplier, Guid>>();

        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var siSameDay = CreatePostedSalesInvoice(_companyId, customerId, "SI-1", _toDate.AddHours(19), 1m, 120m);
        var siNextDay = CreatePostedSalesInvoice(_companyId, customerId, "SI-2", _toDate.AddDays(1), 1m, 880m);
        siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { siSameDay, siNextDay }.AsQueryable());

        var piSameDay = CreatePostedPurchaseInvoice(_companyId, supplierId, "PI-1", _toDate.AddHours(21), 1m, 75m);
        var piNextDay = CreatePostedPurchaseInvoice(_companyId, supplierId, "PI-2", _toDate.AddDays(1), 1m, 300m);
        piRepo.GetQueryableAsync().Returns(new List<PurchaseInvoice> { piSameDay, piNextDay }.AsQueryable());
        peRepo.GetQueryableAsync().Returns(new List<PaymentEntry>().AsQueryable());

        var svc = new StatementOfAccountsAppService(siRepo, piRepo, peRepo, custRepo, supRepo);

        var custStmt = await svc.GetCustomerStatementAsync(customerId, _companyId, _fromDate, _toDate);
        custStmt.TotalDebit.ShouldBe(120m);
        custStmt.Entries.ShouldHaveSingleItem();

        var supStmt = await svc.GetSupplierStatementAsync(supplierId, _companyId, _fromDate, _toDate);
        supStmt.TotalCredit.ShouldBe(75m);
        supStmt.Entries.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TaxSummaryReport_IncludesIntraDayInvoicesOnToDate()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var taxRowRepo = Substitute.For<IRepository<TransactionTaxRow, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var taxCatRepo = Substitute.For<IRepository<TaxCategory, Guid>>();

        var siSameDay = CreatePostedSalesInvoice(_companyId, Guid.NewGuid(), "SI-1", _toDate.AddHours(22), 1m, 1000m, 80m);
        var siNextDay = CreatePostedSalesInvoice(_companyId, Guid.NewGuid(), "SI-2", _toDate.AddDays(1), 1m, 2000m, 160m);
        var piSameDay = CreatePostedPurchaseInvoice(_companyId, Guid.NewGuid(), "PI-1", _toDate.AddHours(23), 1m, 500m, 40m);

        siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { siSameDay, siNextDay }.AsQueryable());
        piRepo.GetQueryableAsync().Returns(new List<PurchaseInvoice> { piSameDay }.AsQueryable());

        var svc = new TaxSummaryReportAppService(siRepo, piRepo, taxRowRepo, itemRepo, taxCatRepo);

        var summary = await svc.GetTaxSummaryAsync(_companyId, _fromDate, _toDate);
        summary.OutputTax.ShouldBe(80m);
        summary.InputTax.ShouldBe(40m);

        var sst02 = await svc.GetSst02FilingDataAsync(_companyId, _fromDate, _toDate);
        sst02.TotalOutputTax.ShouldBe(80m);
        sst02.InputTaxCredit.ShouldBe(40m);
    }

    [Fact]
    public async Task RegistersAndGrossProfit_IncludeIntraDayInvoicesOnToDate()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var siSameDay = new SalesInvoice(Guid.NewGuid(), _companyId, customerId, "SI-1", _toDate.AddHours(23).AddMinutes(30));
        siSameDay.AddItem(itemId, "Widget", 2m, 100m, 0m);
        siSameDay.Submit();
        siSameDay.Post();

        var siNextDay = new SalesInvoice(Guid.NewGuid(), _companyId, customerId, "SI-2", _toDate.AddDays(1));
        siNextDay.AddItem(itemId, "Widget", 9m, 100m, 0m);
        siNextDay.Submit();
        siNextDay.Post();

        siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { siSameDay, siNextDay }.AsQueryable());

        var piSameDay = CreatePostedPurchaseInvoice(_companyId, supplierId, "PI-1", _toDate.AddHours(20), 1m, 150m);
        piRepo.GetQueryableAsync().Returns(new List<PurchaseInvoice> { piSameDay }.AsQueryable());
        peRepo.GetQueryableAsync().Returns(new List<PaymentEntry>().AsQueryable());

        custRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Customer> { new(customerId, _companyId, "Acme Customer") });
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Item> { new(itemId, _companyId, "ITEM-1", "Widget", ItemType.Goods) });
        whRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Warehouse>());

        var salesReg = new SalesRegisterAppService(siRepo, peRepo);
        var salesReport = await salesReg.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        salesReport.Items.ShouldHaveSingleItem();

        var purchaseReg = new PurchaseRegisterAppService(piRepo, peRepo);
        var purchaseReport = await purchaseReg.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        purchaseReport.Items.ShouldHaveSingleItem();

        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var gpService = new GrossProfitService(new StockValuationService(sleRepo, itemRepo, Substitute.For<ISettingProvider>()));
        var gpApp = new GrossProfitReportAppService(siRepo, gpService, custRepo, itemRepo, whRepo);
        var gpReport = await gpApp.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        gpReport.Items.ShouldHaveSingleItem();
        gpReport.TotalRevenue.ShouldBe(200m);
    }
}
