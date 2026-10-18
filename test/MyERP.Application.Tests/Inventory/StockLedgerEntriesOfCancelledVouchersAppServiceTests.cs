using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class StockLedgerEntriesOfCancelledVouchersAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<StockReconciliation, Guid> _stockReconciliationRepo = Substitute.For<IRepository<StockReconciliation, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepostItemValuationAppService _repostService = Substitute.For<IRepostItemValuationAppService>();

    private readonly StockLedgerEntriesOfCancelledVouchersAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public StockLedgerEntriesOfCancelledVouchersAppServiceTests()
    {
        _appService = new StockLedgerEntriesOfCancelledVouchersAppService(
            _sleRepo,
            _stockEntryRepo,
            _purchaseReceiptRepo,
            _deliveryNoteRepo,
            _salesInvoiceRepo,
            _purchaseInvoiceRepo,
            _stockReconciliationRepo,
            _bundleRepo,
            _itemRepo,
            _warehouseRepo,
            _repostService);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoCancelledVouchersExist_ReturnsEmptyReport()
    {
        // Arrange
        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(_itemId, 10, targetWarehouseId: _warehouseId, valuationRate: 50);
        se.Submit(); // Status = Submitted

        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 10, 50, 10, 500)
        {
            VoucherType = "StockEntry",
            VoucherId = seId,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));
        _stockEntryRepo.GetListAsync(Arg.Any<Expression<Func<StockEntry, bool>>>()).Returns(Task.FromResult(new List<StockEntry>()));

        // Act
        var result = await _appService.GetReportAsync(new StockLedgerEntriesOfCancelledVouchersFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalUncancelledEntries.ShouldBe(0);
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WhenCancelledVouchersExist_ReturnsOrphanedEntries()
    {
        // Arrange
        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow) { EntryNumber = "STE-001" };
        se.AddItem(_itemId, 10, targetWarehouseId: _warehouseId, valuationRate: 50);
        se.Submit();
        se.Post();
        se.Cancel(); // Status = Cancelled

        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 10, 50, 10, 500)
        {
            VoucherType = "StockEntry",
            VoucherId = seId,
            StockValueDifference = 500,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));
        _stockEntryRepo.GetListAsync(Arg.Any<Expression<Func<StockEntry, bool>>>()).Returns(Task.FromResult(new List<StockEntry> { se }));

        var item = new Item(_itemId, _companyId, "ITEM-01", "Test Item", ItemType.Goods);
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(Task.FromResult(new List<Item> { item }));

        var wh = new Warehouse(_warehouseId, _companyId, "Main Warehouse");
        _warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(Task.FromResult(new List<Warehouse> { wh }));

        _bundleRepo.GetListAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, bool>>>()).Returns(Task.FromResult(new List<SerialAndBatchBundle>()));

        // Act
        var result = await _appService.GetReportAsync(new StockLedgerEntriesOfCancelledVouchersFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalUncancelledEntries.ShouldBe(1);
        result.DistinctCancelledVouchersCount.ShouldBe(1);
        result.TotalDiscrepancyQty.ShouldBe(10);
        result.TotalDiscrepancyValue.ShouldBe(500);

        var row = result.Rows[0];
        row.VoucherType.ShouldBe("StockEntry");
        row.VoucherId.ShouldBe(seId);
        row.VoucherNo.ShouldBe("STE-001");
        row.QuantityChange.ShouldBe(10);
        row.StockValueDifference.ShouldBe(500);
        row.ItemCode.ShouldBe("ITEM-01");
        row.WarehouseName.ShouldBe("Main Warehouse");
    }

    [Fact]
    public async Task FixUncancelledEntriesAsync_CancelsSles_CancelsBundles_AndQueuesValuationRepost()
    {
        // Arrange
        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow) { EntryNumber = "STE-001" };
        se.AddItem(_itemId, 10, targetWarehouseId: _warehouseId, valuationRate: 50);
        se.Submit();
        se.Post();
        se.Cancel(); // Status = Cancelled

        _stockEntryRepo.FindAsync(seId).Returns(Task.FromResult<StockEntry?>(se));

        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 10, 50, 10, 500)
        {
            VoucherType = "StockEntry",
            VoucherId = seId,
            IsCancelled = false
        };
        _sleRepo.GetListAsync(Arg.Any<Expression<Func<StockLedgerEntry, bool>>>()).Returns(Task.FromResult(new List<StockLedgerEntry> { sle }));

        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _itemId, _warehouseId, BundleTransactionType.Inward, "StockEntry", seId, DateTime.UtcNow);
        _bundleRepo.GetListAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, bool>>>()).Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }));

        _repostService.CreateAsync(Arg.Any<CreateRepostItemValuationDto>())
            .Returns(Task.FromResult(new RepostItemValuationDto()));

        // Act
        var result = await _appService.FixUncancelledEntriesAsync(new FixUncancelledEntriesInputDto
        {
            VoucherKeys = new List<CancelledVoucherKeyDto>
            {
                new CancelledVoucherKeyDto { VoucherType = "StockEntry", VoucherId = seId }
            }
        });

        // Assert
        result.ShouldNotBeNull();
        result.CancelledSleCount.ShouldBe(1);
        result.CancelledBundleCount.ShouldBe(1);
        result.RepostItemValuationCreatedCount.ShouldBe(1);

        sle.IsCancelled.ShouldBeTrue();
        await _sleRepo.Received(1).UpdateAsync(sle);

        bundle.IsCancelled.ShouldBeTrue();
        bundle.Status.ShouldBe(DocumentStatus.Cancelled);
        await _bundleRepo.Received(1).UpdateAsync(bundle);

        await _repostService.Received(1).CreateAsync(Arg.Is<CreateRepostItemValuationDto>(dto =>
            dto.CompanyId == _companyId &&
            dto.ItemId == _itemId &&
            dto.WarehouseId == _warehouseId &&
            dto.BasedOn == 0 &&
            dto.RepostGlEntries == true &&
            dto.VoucherType == "StockEntry" &&
            dto.VoucherId == seId));
    }

    [Fact]
    public async Task FixUncancelledEntriesAsync_WhenVoucherNotCancelled_SkipsRepair()
    {
        // Arrange
        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(_itemId, 10, targetWarehouseId: _warehouseId, valuationRate: 50);
        se.Submit(); // Status = Submitted, NOT Cancelled

        _stockEntryRepo.FindAsync(seId).Returns(Task.FromResult<StockEntry?>(se));

        // Act
        var result = await _appService.FixUncancelledEntriesAsync(new FixUncancelledEntriesInputDto
        {
            VoucherKeys = new List<CancelledVoucherKeyDto>
            {
                new CancelledVoucherKeyDto { VoucherType = "StockEntry", VoucherId = seId }
            }
        });

        // Assert
        result.ShouldNotBeNull();
        result.CancelledSleCount.ShouldBe(0);
        result.CancelledBundleCount.ShouldBe(0);
        result.RepostItemValuationCreatedCount.ShouldBe(0);
        result.Messages.Count.ShouldBe(1);
        result.Messages[0].ShouldContain("not cancelled");

        await _sleRepo.DidNotReceive().UpdateAsync(Arg.Any<StockLedgerEntry>());
        await _bundleRepo.DidNotReceive().UpdateAsync(Arg.Any<SerialAndBatchBundle>());
        await _repostService.DidNotReceive().CreateAsync(Arg.Any<CreateRepostItemValuationDto>());
    }
}
