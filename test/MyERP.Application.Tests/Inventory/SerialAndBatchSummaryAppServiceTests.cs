using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class SerialAndBatchSummaryAppServiceTests
{
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<StockReconciliation, Guid> _stockReconciliationRepo = Substitute.For<IRepository<StockReconciliation, Guid>>();

    private readonly SerialAndBatchSummaryAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public SerialAndBatchSummaryAppServiceTests()
    {
        _appService = new SerialAndBatchSummaryAppService(
            _bundleRepo,
            _itemRepo,
            _warehouseRepo,
            _batchRepo,
            _stockEntryRepo,
            _purchaseReceiptRepo,
            _deliveryNoteRepo,
            _salesInvoiceRepo,
            _purchaseInvoiceRepo,
            _stockReconciliationRepo);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoBundles_ReturnsEmptyReport()
    {
        // Arrange
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialAndBatchSummaryFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalBundles.ShouldBe(0);
        result.TotalEntries.ShouldBe(0);
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WithSerialAndBatchEntries_ReturnsDetailedRows()
    {
        // Arrange
        var voucherId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var bundle = new SerialAndBatchBundle(
            bundleId, _companyId, _itemId, _warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", voucherId,
            DateTime.UtcNow);

        var entry1 = new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 150m, serialNo: "SN-1001");
        var entry2 = new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 5, 200m, batchId: batchId);
        bundle.AddEntry(entry1);
        bundle.AddEntry(entry2);

        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-01", "Hardware Item", ItemType.Goods);
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        var wh = new Warehouse(_warehouseId, _companyId, "Stores WH");
        _warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>())
            .Returns(Task.FromResult(new List<Warehouse> { wh }));

        var batch = new Batch(batchId, _itemId, "BATCH-2026-A");
        _batchRepo.GetListAsync(Arg.Any<Expression<Func<Batch, bool>>>())
            .Returns(Task.FromResult(new List<Batch> { batch }));

        var pr = new PurchaseReceipt(voucherId, _companyId, Guid.NewGuid(), _warehouseId, "PR-2026-0001", DateTime.UtcNow);
        _purchaseReceiptRepo.GetListAsync(Arg.Any<Expression<Func<PurchaseReceipt, bool>>>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }));

        // Act
        var result = await _appService.GetReportAsync(new SerialAndBatchSummaryFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalBundles.ShouldBe(1);
        result.TotalEntries.ShouldBe(2);
        result.TotalInwardQty.ShouldBe(6); // 1 + 5
        result.TotalStockValue.ShouldBe(1150m); // 1*150 + 5*200

        result.Rows.Count.ShouldBe(2);

        var row1 = result.Rows[0];
        row1.BundleId.ShouldBe(bundleId);
        row1.VoucherType.ShouldBe("PurchaseReceipt");
        row1.VoucherNo.ShouldBe("PR-2026-0001");
        row1.ItemCode.ShouldBe("ITEM-01");
        row1.WarehouseName.ShouldBe("Stores WH");
        row1.TypeOfTransaction.ShouldBe("Inward");
        row1.SerialNo.ShouldBe("SN-1001");
        row1.Quantity.ShouldBe(1);
        row1.IncomingRate.ShouldBe(150m);
        row1.StockValueDifference.ShouldBe(150m);

        var row2 = result.Rows[1];
        row2.BatchNo.ShouldBe("BATCH-2026-A");
        row2.Quantity.ShouldBe(5);
        row2.IncomingRate.ShouldBe(200m);
        row2.StockValueDifference.ShouldBe(1000m);
    }
}
