using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class ReceivedItemsToBeBilledAppServiceTests
{
    private readonly IRepository<PurchaseReceipt, Guid> _receiptRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
    private readonly IRepository<Project, Guid> _projectRepo = Substitute.For<IRepository<Project, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemIdNonStock = Guid.NewGuid();
    private readonly Guid _poId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    private readonly Company _company;
    private readonly Supplier _supplier;
    private readonly Item _item1;
    private readonly Item _itemNonStock;
    private readonly Project _project;

    public ReceivedItemsToBeBilledAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Manufacturing") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _supplier = new Supplier(_supplierId, _companyId, "Premier Steel Sdn Bhd") { SupplierCode = "SUPP-001" };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { _supplier }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "RAW-001", "Steel Pipe", ItemType.Goods)
        {
            MaintainStock = true
        };
        _itemNonStock = new Item(_itemIdNonStock, _companyId, "SERV-002", "Consulting", ItemType.Service)
        {
            MaintainStock = false
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item1, _itemNonStock }.AsQueryable()));

        _project = new Project(_projectId, _companyId, "PROJ-101", "Plant Expansion");
        _projectRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Project> { _project }.AsQueryable()));

        var po = new PurchaseOrder(_poId, _companyId, _supplierId, "PO-001", new DateTime(2026, 5, 1))
        {
            ProjectId = _projectId
        };
        _purchaseOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseOrder> { po }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_UnbilledReceipt_AppearsWithPendingAmount()
    {
        // Arrange: 5 units @ 200 = 1000, unbilled
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", new DateTime(2026, 6, 1))
        {
            PurchaseOrderId = _poId
        };
        pr.AddItem(_itemId1, "Steel Pipe", 5m, 200m, 0m);
        pr.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var appService = new ReceivedItemsToBeBilledAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _purchaseOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetReceivedItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ReceiptNumber.ShouldBe("PR-001");
        row.SupplierName.ShouldBe("Premier Steel Sdn Bhd");
        row.ItemCode.ShouldBe("RAW-001");
        row.Amount.ShouldBe(1000m);
        row.BilledAmount.ShouldBe(0m);
        row.ReturnedAmount.ShouldBe(0m);
        row.PendingAmount.ShouldBe(1000m);
        row.ProjectName.ShouldBe("Plant Expansion");
        result.TotalPendingAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_FullyBilledReceipt_DropsOut()
    {
        // Arrange: 5 units @ 200, fully billed (BilledQty = 5)
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-002", new DateTime(2026, 6, 1));
        pr.AddItem(_itemId1, "Steel Pipe", 5m, 200m, 0m);
        pr.Items.Last().BilledQty = 5m;
        pr.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var appService = new ReceivedItemsToBeBilledAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _purchaseOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetReceivedItemsToBeBilledInput
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
        // Arrange: 10 accepted @ 100 = 1000, 3 billed (300), 2 rejected (200) -> 500 pending
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-003", new DateTime(2026, 6, 1));
        var rejectedWarehouseId = Guid.NewGuid();
        pr.AddItem(_itemId1, "Steel Pipe", 10m, 100m, 0m, rejectedQty: 2m, rejectedWarehouseId: rejectedWarehouseId);
        var item = pr.Items.Last();
        item.BilledQty = 3m;
        pr.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var appService = new ReceivedItemsToBeBilledAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _purchaseOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetReceivedItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.Amount.ShouldBe(1000m);
        row.BilledAmount.ShouldBe(300m);
        row.ReturnedAmount.ShouldBe(200m);
        row.PendingAmount.ShouldBe(500m);
        result.TotalPendingAmount.ShouldBe(500m);
    }

    [Fact]
    public async Task GetReportAsync_CutoffDateFilter_ExcludesLaterReceipts()
    {
        // Arrange: posting date 15 July 2026, cutoff 30 June 2026
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-004", new DateTime(2026, 7, 15));
        pr.AddItem(_itemId1, "Steel Pipe", 5m, 200m, 0m);
        pr.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var appService = new ReceivedItemsToBeBilledAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _purchaseOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetReceivedItemsToBeBilledInput
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
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-005", new DateTime(2026, 6, 1));
        pr.AddItem(_itemIdNonStock, "Consulting", 1m, 500m, 0m);
        pr.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var appService = new ReceivedItemsToBeBilledAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _purchaseOrderRepo, _projectRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetReceivedItemsToBeBilledInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
    }
}
