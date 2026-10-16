using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class SalesPersonTargetVarianceAppServiceTests
{
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly Guid _salesPersonId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    private readonly Company _company;
    private readonly FiscalYear _fiscalYear;
    private readonly SalesPerson _salesPerson;
    private readonly ItemGroup _itemGroup;
    private readonly Item _item;

    public SalesPersonTargetVarianceAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _fiscalYear = new FiscalYear(
            _fiscalYearId,
            _companyId,
            "2026",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31));
        _fiscalYearRepo.FindAsync(_fiscalYearId).Returns(Task.FromResult<FiscalYear?>(_fiscalYear));

        _itemGroup = new ItemGroup(_itemGroupId, "Electronics");
        _itemGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemGroup> { _itemGroup }.AsQueryable()));

        _item = new Item(_itemId, _companyId, "ELEC-001", "Laptop Pro", ItemType.Goods)
        {
            ItemGroupId = _itemGroupId,
            ItemGroup = "Electronics",
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item }.AsQueryable()));

        _salesPerson = new SalesPerson(_salesPersonId, "Sarah Jenkins");
        _salesPerson.AddTarget(_fiscalYearId, 100m, 50000m, _itemGroupId);

        _salesPersonRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesPerson, object>>[]>())
            .Returns(Task.FromResult(new List<SalesPerson> { _salesPerson }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnTargets_WhenNoTransactions()
    {
        // Arrange
        _salesInvoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry>().AsQueryable()));

        var service = new SalesPersonTargetVarianceAppService(
            _salesPersonRepo,
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _itemRepo,
            _itemGroupRepo,
            _fiscalYearRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonTargetVarianceInput
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId,
            DocType = "Sales Invoice",
            TargetOn = "Amount",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SalesPersonName.ShouldBe("Sarah Jenkins");
        row.ItemGroupName.ShouldBe("Electronics");
        row.Target.ShouldBe(50000m);
        row.Achieved.ShouldBe(0m);
        row.Variance.ShouldBe(-50000m);
        row.AchievementPercentage.ShouldBe(0m);

        result.TotalTarget.ShouldBe(50000m);
        result.TotalAchieved.ShouldBe(0m);
        result.TotalVariance.ShouldBe(-50000m);
        result.OverallAchievementPercentage.ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldCalculateVariance_ForAmount_SalesInvoice()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var invoice = new SalesInvoice(invoiceId, _companyId, customerId, "INV-2026-101", new DateTime(2026, 5, 1));
        invoice.AddItem(_itemId, "Laptop Pro", 5, 6000m, 0); // NetTotal = 30000
        invoice.Submit();

        _salesInvoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        // Sarah gets 80% allocation
        var teamEntry = new SalesTeamEntry(Guid.NewGuid(), _salesPersonId, "SalesInvoice", invoiceId, 80m, 30000m, 5m);
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry> { teamEntry }.AsQueryable()));

        var service = new SalesPersonTargetVarianceAppService(
            _salesPersonRepo,
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _itemRepo,
            _itemGroupRepo,
            _fiscalYearRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonTargetVarianceInput
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId,
            DocType = "Sales Invoice",
            TargetOn = "Amount",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Target.ShouldBe(50000m);
        row.Achieved.ShouldBe(24000m); // 30000 * 80%
        row.Variance.ShouldBe(-26000m);
        row.AchievementPercentage.ShouldBe(48m); // 24000 / 50000 * 100

        result.TotalTarget.ShouldBe(50000m);
        result.TotalAchieved.ShouldBe(24000m);
        result.TotalVariance.ShouldBe(-26000m);
        result.OverallAchievementPercentage.ShouldBe(48m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldCalculateVariance_ForQuantity_SalesOrder()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new SalesOrder(orderId, _companyId, customerId, "SO-2026-555", new DateTime(2026, 6, 1));
        order.AddItem(_itemId, "Laptop Pro", 120, 5000m, 0);
        order.Submit();

        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { order }.AsQueryable()));

        // Sarah gets 100% allocation
        var teamEntry = new SalesTeamEntry(Guid.NewGuid(), _salesPersonId, "SalesOrder", orderId, 100m, 600000m, 5m);
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry> { teamEntry }.AsQueryable()));

        var service = new SalesPersonTargetVarianceAppService(
            _salesPersonRepo,
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _itemRepo,
            _itemGroupRepo,
            _fiscalYearRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonTargetVarianceInput
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId,
            DocType = "Sales Order",
            TargetOn = "Quantity",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Target.ShouldBe(100m);
        row.Achieved.ShouldBe(120m);
        row.Variance.ShouldBe(20m);
        row.AchievementPercentage.ShouldBe(120m);

        result.TotalTarget.ShouldBe(100m);
        result.TotalAchieved.ShouldBe(120m);
        result.TotalVariance.ShouldBe(20m);
        result.OverallAchievementPercentage.ShouldBe(120m);
    }
}
