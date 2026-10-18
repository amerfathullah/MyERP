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

public class SerialNoLedgerAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<SerialNo, Guid> _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly SerialNoLedgerAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _serialNoId = Guid.NewGuid();

    public SerialNoLedgerAppServiceTests()
    {
        _appService = new SerialNoLedgerAppService(
            _sleRepo,
            _bundleRepo,
            _serialNoRepo,
            _warehouseRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_EmptyMatches_ReturnsEmptyReport()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Finished Goods WH") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo>().AsQueryable()));
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoLedgerFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalEntries.ShouldBe(0);
        result.InwardCount.ShouldBe(0);
        result.OutwardCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_DirectSerialNoMovement_ReturnsCorrectLedgerRow()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Finished Goods WH") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var serial = new SerialNo(_serialNoId, _itemId, "SN-2026-001", _companyId, _warehouseId);
        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { serial }.AsQueryable()));

        var now = DateTime.UtcNow;
        var sleIn = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now.AddDays(-2), 1m, 1200m, 1m, 1200m)
        {
            SerialNoId = _serialNoId,
            IsCancelled = false,
            VoucherType = "Purchase Receipt",
            VoucherId = Guid.NewGuid()
        };

        var sleOut = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now.AddDays(-1), -1m, 1200m, 0m, 0m)
        {
            SerialNoId = _serialNoId,
            IsCancelled = false,
            VoucherType = "Delivery Note",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sleIn, sleOut }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoLedgerFilterDto
        {
            CompanyId = _companyId,
            SerialNumber = "SN-2026-001"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalEntries.ShouldBe(2);
        result.InwardCount.ShouldBe(1);
        result.OutwardCount.ShouldBe(1);

        var outRow = result.Rows.First(r => r.VoucherType == "Delivery Note");
        outRow.SerialNumber.ShouldBe("SN-2026-001");
        outRow.Qty.ShouldBe(-1m);
        outRow.Status.ShouldBe("Delivered");
        outRow.PartyType.ShouldBe("Customer");

        var inRow = result.Rows.First(r => r.VoucherType == "Purchase Receipt");
        inRow.Qty.ShouldBe(1m);
        inRow.Status.ShouldBe("Active");
        inRow.PartyType.ShouldBe("Supplier");
    }

    [Fact]
    public async Task GetReportAsync_BundleSerialNoMovement_ReturnsCorrectLedgerRow()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Finished Goods WH") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo>().AsQueryable()));

        var bundleId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var sle = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now, -1m, 1500m, 0m, 0m)
        {
            SerialAndBatchBundleId = bundleId,
            IsCancelled = false,
            VoucherType = "Sales Invoice",
            VoucherId = Guid.NewGuid()
        };
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        var bundle = new SerialAndBatchBundle(bundleId, _companyId, _itemId, _warehouseId, BundleTransactionType.Outward, "Sales Invoice", sle.VoucherId!.Value, now.Date)
        {
            IsCancelled = false
        };
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1m, 1500m, serialNo: "SN-2026-BUNDLE-01"));

        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoLedgerFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalEntries.ShouldBe(1);
        result.Rows[0].SerialNumber.ShouldBe("SN-2026-BUNDLE-01");
        result.Rows[0].Qty.ShouldBe(-1m);
        result.Rows[0].Status.ShouldBe("Delivered");
        result.Rows[0].PartyType.ShouldBe("Customer");
    }
}
