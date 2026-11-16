using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Sales;

/// <summary>
/// Regression tests verifying Sales and Purchasing reports include intra-day transactions
/// up to 23:59:59.9999999 on ToDate and exclude next-day entries.
/// </summary>
public class SalesPurchasingReportsToDateCutoffTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly DateTime _fromDate = new(2026, 6, 1);
    private readonly DateTime _toDate = new(2026, 6, 30);

    private static SalesInvoice CreatePostedInvoice(Guid companyId, Guid customerId, Guid itemId, string number, DateTime issueDate, decimal qty, decimal rate)
    {
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, number, issueDate);
        si.AddItem(itemId, "Widget", qty, rate, 0m);
        si.Submit();
        si.Post();
        return si;
    }

    [Fact]
    public async Task CustomerRevenueAndItemSales_IncludeIntraDayInvoicesOnToDate()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();

        var custId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var siSameDay = CreatePostedInvoice(_companyId, custId, itemId, "SI-1", _toDate.AddHours(23).AddMinutes(55), 3m, 100m);
        var siNextDay = CreatePostedInvoice(_companyId, custId, itemId, "SI-2", _toDate.AddDays(1), 10m, 100m);
        siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { siSameDay, siNextDay }.AsQueryable());

        custRepo.GetQueryableAsync().Returns(new List<Customer> { new(custId, _companyId, "Acme Customer") }.AsQueryable());

        var custRevSvc = new CustomerRevenueAppService(siRepo, custRepo);
        var custRev = await custRevSvc.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        custRev.Items.ShouldHaveSingleItem();
        custRev.TotalRevenue.ShouldBe(300m);

        var itemSalesSvc = new ItemSalesAppService(siRepo);
        var itemSales = await itemSalesSvc.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        itemSales.Items.ShouldHaveSingleItem();
        itemSales.TotalRevenue.ShouldBe(300m);
        itemSales.TotalQty.ShouldBe(3m);
    }

    [Fact]
    public async Task SalesCommissionReport_IncludesIntraDayInvoicesOnToDate()
    {
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var teamRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();

        var siSameDay = CreatePostedInvoice(_companyId, Guid.NewGuid(), Guid.NewGuid(), "SI-1", _toDate.AddHours(22).AddMinutes(30), 2m, 250m);
        var siNextDay = CreatePostedInvoice(_companyId, Guid.NewGuid(), Guid.NewGuid(), "SI-2", _toDate.AddDays(1), 5m, 250m);

        siRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { siSameDay, siNextDay }.AsQueryable());
        teamRepo.GetQueryableAsync().Returns(new List<SalesTeamEntry>().AsQueryable());

        var svc = new SalesCommissionReportAppService(teamRepo, siRepo);
        var report = await svc.GetReportAsync(_companyId, _fromDate, _toDate);

        report.InvoiceCount.ShouldBe(1);
        report.TotalRevenue.ShouldBe(500m);
    }

    [Fact]
    public async Task OrderAnalysisReports_IncludeIntraDayOrdersOnToDate()
    {
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        companyRepo.FindAsync(_companyId).Returns(new Company(_companyId, "Acme Co") { CurrencyCode = "MYR" });
        custRepo.GetQueryableAsync().Returns(new List<Customer> { new(customerId, _companyId, "Buyer") }.AsQueryable());
        itemRepo.GetQueryableAsync().Returns(new List<Item> { new(itemId, _companyId, "ITEM-1", "Widget", ItemType.Goods) }.AsQueryable());
        whRepo.GetQueryableAsync().Returns(new List<Warehouse>().AsQueryable());

        var soSameDay = new SalesOrder(Guid.NewGuid(), _companyId, customerId, "SO-1", _toDate.AddHours(23).AddMinutes(10));
        soSameDay.AddItem(itemId, "Widget", 4m, 50m, 0m);
        soSameDay.Submit();

        var soNextDay = new SalesOrder(Guid.NewGuid(), _companyId, customerId, "SO-2", _toDate.AddDays(1));
        soNextDay.AddItem(itemId, "Widget", 6m, 50m, 0m);
        soNextDay.Submit();

        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(new List<SalesOrder> { soSameDay, soNextDay }.AsQueryable());

        var soSvc = new SalesOrderAnalysisAppService(soRepo, custRepo, itemRepo, whRepo, companyRepo);
        var soReport = await soSvc.GetAnalysisAsync(new GetSalesOrderAnalysisDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        soReport.Rows.ShouldHaveSingleItem();
        soReport.TotalQty.ShouldBe(4m);
        soReport.TotalAmount.ShouldBe(200m);

        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var supRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supRepo.GetQueryableAsync().Returns(new List<Supplier> { new(supplierId, _companyId, "Vendor") }.AsQueryable());

        var poSameDay = new PurchaseOrder(Guid.NewGuid(), _companyId, supplierId, "PO-1", _toDate.AddHours(23).AddMinutes(20));
        poSameDay.AddItem(itemId, "Widget", 5m, 40m, 0m);
        poSameDay.Submit();

        var poNextDay = new PurchaseOrder(Guid.NewGuid(), _companyId, supplierId, "PO-2", _toDate.AddDays(1));
        poNextDay.AddItem(itemId, "Widget", 9m, 40m, 0m);
        poNextDay.Submit();

        poRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(new List<PurchaseOrder> { poSameDay, poNextDay }.AsQueryable());
        poRepo.GetQueryableAsync().Returns(new List<PurchaseOrder> { poSameDay, poNextDay }.AsQueryable());

        var poSvc = new PurchaseOrderAnalysisAppService(poRepo, supRepo, itemRepo, whRepo, companyRepo);
        var poReport = await poSvc.GetAnalysisAsync(new GetPurchaseOrderAnalysisDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        poReport.Rows.ShouldHaveSingleItem();
        poReport.TotalQty.ShouldBe(5m);
        poReport.TotalAmount.ShouldBe(200m);

        var perfSvc = new SupplierDeliveryPerformanceAppService(poRepo, supRepo);
        var perfReport = await perfSvc.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        perfReport.Suppliers.ShouldHaveSingleItem();
        perfReport.TotalOrders.ShouldBe(1);
    }
}
