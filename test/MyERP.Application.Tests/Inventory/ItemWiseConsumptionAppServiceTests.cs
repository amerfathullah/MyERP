using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class ItemWiseConsumptionAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();

    private readonly ItemWiseConsumptionAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemWiseConsumptionAppServiceTests()
    {
        _appService = new ItemWiseConsumptionAppService(
            _sleRepo,
            _stockEntryRepo,
            _itemRepo,
            _supplierRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesConsumedAndDeliveredQuantitiesAndAmounts()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var item = new Item(itemId, _companyId, "ITEM-001", "Component A", ItemType.Goods)
        {
            Uom = "Nos",
            Description = "Standard component A"
        };

        var supplierId = Guid.NewGuid();
        var supplier = new Supplier(supplierId, _companyId, "Acme Parts");
        item.Suppliers.Add(new ItemSupplier(Guid.NewGuid(), itemId, supplierId, "ACME-001"));

        // Delivery Note entry
        var dnEntry = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-5), -10m, 15m, 40m, 600m)
        {
            VoucherType = "DeliveryNote",
            StockValueDifference = -150m
        };

        // Manufacture consumption entry
        var issueEntry = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-2), -5m, 15m, 35m, 525m)
        {
            VoucherType = "StockEntry",
            StockValueDifference = -75m
        };

        _stockEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { dnEntry, issueEntry }.AsQueryable()));

        _itemRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Item, object>>[]>())
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _supplierRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemWiseConsumptionRequestDto
        {
            FromDate = today.AddDays(-10),
            ToDate = today,
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.DeliveredQty.ShouldBe(10m);
        row.DeliveredAmount.ShouldBe(150m);
        row.ConsumedQty.ShouldBe(5m);
        row.ConsumedAmount.ShouldBe(75m);
        row.TotalQty.ShouldBe(15m);
        row.TotalAmount.ShouldBe(225m);
        row.Suppliers.ShouldBe("Acme Parts");

        result.GrandTotalDeliveredQty.ShouldBe(10m);
        result.GrandTotalConsumedQty.ShouldBe(5m);
        result.GrandTotalQty.ShouldBe(15m);
        result.GrandTotalAmount.ShouldBe(225m);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesMaterialTransferStockEntries()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var item = new Item(itemId, _companyId, "ITEM-002", "Transfer Material", ItemType.Goods);

        var transferEntry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, today.AddDays(-3))
        {
            EntryNumber = "STE-TR-001"
        };

        var transferSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-3), -20m, 10m, 10m, 100m)
        {
            VoucherType = "StockEntry",
            VoucherId = transferEntry.Id,
            StockValueDifference = -200m
        };

        _stockEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockEntry> { transferEntry }.AsQueryable()));

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { transferSle }.AsQueryable()));

        _itemRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Item, object>>[]>())
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _supplierRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Supplier>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemWiseConsumptionRequestDto
        {
            FromDate = today.AddDays(-10),
            ToDate = today,
            CompanyId = _companyId
        });

        // Assert: Material transfer excluded, 0 rows returned
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.GrandTotalQty.ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoMovements_ReturnsEmptyReport()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;

        _stockEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemWiseConsumptionRequestDto
        {
            FromDate = today.AddDays(-10),
            ToDate = today
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalItems.ShouldBe(0);
        result.GrandTotalAmount.ShouldBe(0m);
    }
}
