using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class AvailableSerialNoReportAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<SerialNo, Guid> _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();

    private readonly AvailableSerialNoReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public AvailableSerialNoReportAppServiceTests()
    {
        _appService = new AvailableSerialNoReportAppService(
            _sleRepo,
            _itemRepo,
            _warehouseRepo,
            _serialNoRepo,
            _bundleRepo);
    }

    [Fact]
    public async Task GetReportAsync_InwardAndOutwardTransactions_CorrectlyMaintainsActiveSerialNos()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sn1Id = Guid.NewGuid();
        var sn2Id = Guid.NewGuid();
        var sn1 = new SerialNo(sn1Id, _itemId, "SN-001", _companyId);
        var sn2 = new SerialNo(sn2Id, _itemId, "SN-002", _companyId);
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>()).Returns(Task.FromResult(new List<SerialNo> { sn1, sn2 }));

        var now = DateTime.UtcNow;
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-2), 1m, 1000m, 1m, 1000m)
        {
            SerialNoId = sn1Id,
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-1.5), 1m, 1000m, 2m, 2000m)
        {
            SerialNoId = sn2Id,
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };
        var sle3 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-1), -1m, 1000m, 1m, 1000m)
        {
            SerialNoId = sn1Id,
            VoucherType = "DeliveryNote",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle1, sle2, sle3 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new AvailableSerialNoFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId,
            ItemId = _itemId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalTransactions.ShouldBe(3);
        result.TotalInQty.ShouldBe(2m);
        result.TotalOutQty.ShouldBe(1m);
        result.FinalBalanceQty.ShouldBe(1m);
        result.FinalAvailableSerialCount.ShouldBe(1);
        result.DiscrepancyCount.ShouldBe(0);

        var row1 = result.Rows[0];
        row1.BalanceSerialNos.ShouldBe(new[] { "SN-001" });
        row1.BalanceSerialCount.ShouldBe(1);

        var row2 = result.Rows[1];
        row2.BalanceSerialNos.ShouldBe(new[] { "SN-001", "SN-002" });
        row2.BalanceSerialCount.ShouldBe(2);

        var row3 = result.Rows[2];
        row3.BalanceSerialNos.ShouldBe(new[] { "SN-002" });
        row3.BalanceSerialCount.ShouldBe(1);
        row3.HasDiscrepancy.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportAsync_SerialAndBatchBundleResolution_ExtractsBundleSerials()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>()).Returns(Task.FromResult(new List<SerialNo>()));

        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, _companyId, _itemId, _warehouseId, BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1m, 500m, "SN-BUNDLE-A"));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1m, 500m, "SN-BUNDLE-B"));

        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }.AsQueryable()));

        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 2m, 500m, 2m, 1000m)
        {
            SerialAndBatchBundleId = bundleId,
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new AvailableSerialNoFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalTransactions.ShouldBe(1);
        result.Rows[0].SerialNosMoved.ShouldContain("SN-BUNDLE-A");
        result.Rows[0].SerialNosMoved.ShouldContain("SN-BUNDLE-B");
        result.Rows[0].BalanceSerialCount.ShouldBe(2);
        result.Rows[0].HasDiscrepancy.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportAsync_DiscrepancyBetweenQtyAndSerialCount_FlagsDiscrepancy()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sn1Id = Guid.NewGuid();
        var sn1 = new SerialNo(sn1Id, _itemId, "SN-001", _companyId);
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>()).Returns(Task.FromResult(new List<SerialNo> { sn1 }));

        // BalanceQty = 2, but only 1 serial number tracked
        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 2m, 1000m, 2m, 2000m)
        {
            SerialNoId = sn1Id,
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new AvailableSerialNoFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.DiscrepancyCount.ShouldBe(1);
        result.Rows[0].HasDiscrepancy.ShouldBeTrue();
        result.Rows[0].BalanceQty.ShouldBe(2m);
        result.Rows[0].BalanceSerialCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetReportAsync_DateWindowFiltering_MaintainsRunningBalancePriorToFromDate()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sn1Id = Guid.NewGuid();
        var sn2Id = Guid.NewGuid();
        var sn1 = new SerialNo(sn1Id, _itemId, "SN-001", _companyId);
        var sn2 = new SerialNo(sn2Id, _itemId, "SN-002", _companyId);
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>()).Returns(Task.FromResult(new List<SerialNo> { sn1, sn2 }));

        var baseDate = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        // Day 1: Inward SN-001
        var sleDay1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, baseDate, 1m, 1000m, 1m, 1000m)
        {
            SerialNoId = sn1Id,
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };
        // Day 2: Inward SN-002
        var sleDay2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, baseDate.AddDays(1), 1m, 1000m, 2m, 2000m)
        {
            SerialNoId = sn2Id,
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sleDay1, sleDay2 }.AsQueryable()));

        // Act: filter FromDate = Day 2
        var result = await _appService.GetReportAsync(new AvailableSerialNoFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId,
            FromDate = baseDate.AddDays(1)
        });

        // Assert: only Day 2 is returned, but running balance contains BOTH SN-001 and SN-002
        result.ShouldNotBeNull();
        result.TotalTransactions.ShouldBe(1);
        result.Rows[0].BalanceSerialNos.ShouldBe(new[] { "SN-001", "SN-002" });
        result.Rows[0].BalanceSerialCount.ShouldBe(2);
    }
}
