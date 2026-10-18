using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class DeliveredItemsToBeBilledAppServiceTests
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Project, Guid> _projectRepo = Substitute.For<IRepository<Project, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemIdNonStock = Guid.NewGuid();
    private readonly Guid _salesOrderId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    private readonly Company _company;
    private readonly Customer _customer;
    private readonly Item _item1;
    private readonly Item _itemNonStock;
    private readonly Project _project;

    public DeliveredItemsToBeBilledAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Logistics") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customer = new Customer(_customerId, _companyId, "Global Traders") { CustomerCode = "CUST-GT" };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "ITEM-001", "Industrial Valve", ItemType.Goods)
        {
            MaintainStock = true
        };
        _itemNonStock = new Item(_itemIdNonStock, _companyId, "SERV-001", "Installation Service", ItemType.Service)
        {
            MaintainStock = false
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item1, _itemNonStock }.AsQueryable()));

        _project = new Project(_projectId, _companyId, "PROJ-001", "Project Apollo");
        _projectRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Project> { _project }.AsQueryable()));

        var so = new SalesOrder(_salesOrderId, _companyId, _customerId, "SO-001", new DateTime(2026, 5, 1))
        {
            ProjectId = _projectId
        };
        _salesOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_UnbilledDeliveryNote_AppearsWithPendingAmount()
    {
        // Arrange: 5 units @ 300 = 1500, unbilled
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-001", new DateTime(2026, 6, 1))
        {
            SalesOrderId = _salesOrderId
        };
        dn.AddItem(_itemId1, "Industrial Valve", 5m, 300m, 0m);
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var appService = new DeliveredItemsToBeBilledAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _salesOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetDeliveredItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.DeliveryNumber.ShouldBe("DN-001");
        row.CustomerName.ShouldBe("Global Traders");
        row.ItemCode.ShouldBe("ITEM-001");
        row.Amount.ShouldBe(1500m);
        row.BilledAmount.ShouldBe(0m);
        row.ReturnedAmount.ShouldBe(0m);
        row.PendingAmount.ShouldBe(1500m);
        row.ProjectName.ShouldBe("Project Apollo");
        result.TotalPendingAmount.ShouldBe(1500m);
    }

    [Fact]
    public async Task GetReportAsync_FullyBilledDeliveryNote_DropsOut()
    {
        // Arrange: 5 units @ 300, fully billed (BilledQty = 5)
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-002", new DateTime(2026, 6, 1));
        dn.AddItem(_itemId1, "Industrial Valve", 5m, 300m, 0m);
        dn.Items.Last().BilledQty = 5m;
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var appService = new DeliveredItemsToBeBilledAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _salesOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetDeliveredItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalPendingAmount.ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_PartiallyBilledAndReturned_CalculatesPendingCorrectly()
    {
        // Arrange: 10 units @ 100 = 1000, 4 billed (400), 2 returned (200) -> 400 pending
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-003", new DateTime(2026, 6, 1));
        dn.AddItem(_itemId1, "Industrial Valve", 10m, 100m, 0m);
        var item = dn.Items.Last();
        item.BilledQty = 4m;
        item.ReturnedQty = 2m;
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var appService = new DeliveredItemsToBeBilledAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _salesOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetDeliveredItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.Amount.ShouldBe(1000m);
        row.BilledAmount.ShouldBe(400m);
        row.ReturnedAmount.ShouldBe(200m);
        row.PendingAmount.ShouldBe(400m);
        row.PendingQty.ShouldBe(4m);
        result.TotalPendingAmount.ShouldBe(400m);
    }

    [Fact]
    public async Task GetReportAsync_CutoffDateFilter_ExcludesLaterDeliveryNotes()
    {
        // Arrange: posting date 15 July 2026, cutoff 30 June 2026
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-004", new DateTime(2026, 7, 15));
        dn.AddItem(_itemId1, "Industrial Valve", 5m, 300m, 0m);
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var appService = new DeliveredItemsToBeBilledAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _salesOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetDeliveredItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_NonStockItem_Excluded()
    {
        // Arrange: Service item (MaintainStock == false)
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-005", new DateTime(2026, 6, 1));
        dn.AddItem(_itemIdNonStock, "Installation Service", 1m, 500m, 0m);
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var appService = new DeliveredItemsToBeBilledAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _salesOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetDeliveredItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
    }
}
