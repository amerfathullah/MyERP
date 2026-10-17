using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class WarehouseWiseStockBalanceAppServiceTests
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly WarehouseWiseStockBalanceAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public WarehouseWiseStockBalanceAppServiceTests()
    {
        _appService = new WarehouseWiseStockBalanceAppService(
            _warehouseRepo,
            _sleRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesHierarchicalWarehouseBalancesAndQuantities()
    {
        // Arrange
        var company = new Company(_companyId, "Acme Corp");
        _companyRepo.FindAsync(_companyId).Returns(company);

        var parentWhId = Guid.NewGuid();
        var childWh1Id = Guid.NewGuid();
        var childWh2Id = Guid.NewGuid();

        var parentWh = new Warehouse(parentWhId, _companyId, "All Warehouses")
        {
            WarehouseCode = "WH-ALL",
            IsGroup = true,
            IsActive = true
        };

        var childWh1 = new Warehouse(childWh1Id, _companyId, "Finished Goods")
        {
            WarehouseCode = "WH-FG",
            ParentWarehouseId = parentWhId,
            IsGroup = false,
            IsActive = true
        };

        var childWh2 = new Warehouse(childWh2Id, _companyId, "Raw Materials")
        {
            WarehouseCode = "WH-RM",
            ParentWarehouseId = parentWhId,
            IsGroup = false,
            IsActive = true
        };

        var warehouses = new List<Warehouse> { parentWh, childWh1, childWh2 };
        _warehouseRepo.GetQueryableAsync().Returns(warehouses.AsQueryable());

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, item1Id, childWh1Id, today.AddDays(-2), 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m
        };

        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, item2Id, childWh2Id, today.AddDays(-1), 5m, 100m, 5m, 500m)
        {
            StockValueDifference = 500m
        };

        var sles = new List<StockLedgerEntry> { sle1, sle2 };
        _sleRepo.GetQueryableAsync().Returns(sles.AsQueryable());

        var filter = new WarehouseWiseStockBalanceFilterDto
        {
            CompanyId = _companyId,
            ShowDisabledWarehouses = false
        };

        // Act
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.ShouldNotBeNull();
        result.CompanyName.ShouldBe("Acme Corp");
        result.TotalStockBalance.ShouldBe(1500m);
        result.TotalQuantity.ShouldBe(15m);
        result.TotalWarehouseCount.ShouldBe(3);
        result.ActiveWarehouseCount.ShouldBe(3);

        var parentItem = result.Items.FirstOrDefault(i => i.WarehouseId == parentWhId);
        parentItem.ShouldNotBeNull();
        parentItem.IsGroup.ShouldBeTrue();
        parentItem.Indent.ShouldBe(0);
        parentItem.StockBalance.ShouldBe(1500m);
        parentItem.TotalQuantity.ShouldBe(15m);
        parentItem.DirectStockBalance.ShouldBe(0m);
        parentItem.DirectQuantity.ShouldBe(0m);

        var fgItem = result.Items.FirstOrDefault(i => i.WarehouseId == childWh1Id);
        fgItem.ShouldNotBeNull();
        fgItem.ParentWarehouseId.ShouldBe(parentWhId);
        fgItem.Indent.ShouldBe(1);
        fgItem.StockBalance.ShouldBe(1000m);
        fgItem.TotalQuantity.ShouldBe(10m);
        fgItem.ItemCount.ShouldBe(1);

        var rmItem = result.Items.FirstOrDefault(i => i.WarehouseId == childWh2Id);
        rmItem.ShouldNotBeNull();
        rmItem.ParentWarehouseId.ShouldBe(parentWhId);
        rmItem.Indent.ShouldBe(1);
        rmItem.StockBalance.ShouldBe(500m);
        rmItem.TotalQuantity.ShouldBe(5m);
        rmItem.ItemCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetReportAsync_FiltersDisabledWarehousesUnlessShowDisabledWarehousesIsTrue()
    {
        // Arrange
        var company = new Company(_companyId, "Acme Corp");
        _companyRepo.FindAsync(_companyId).Returns(company);

        var whActiveId = Guid.NewGuid();
        var whDisabledId = Guid.NewGuid();

        var whActive = new Warehouse(whActiveId, _companyId, "Active Warehouse")
        {
            IsActive = true
        };

        var whDisabled = new Warehouse(whDisabledId, _companyId, "Decommissioned Warehouse")
        {
            IsActive = false
        };

        var warehouses = new List<Warehouse> { whActive, whDisabled };
        _warehouseRepo.GetQueryableAsync().Returns(warehouses.AsQueryable());
        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry>().AsQueryable());

        // Act 1: Hide disabled
        var resultWithoutDisabled = await _appService.GetReportAsync(new WarehouseWiseStockBalanceFilterDto
        {
            CompanyId = _companyId,
            ShowDisabledWarehouses = false
        });

        // Act 2: Show disabled
        var resultWithDisabled = await _appService.GetReportAsync(new WarehouseWiseStockBalanceFilterDto
        {
            CompanyId = _companyId,
            ShowDisabledWarehouses = true
        });

        // Assert
        resultWithoutDisabled.Items.Count.ShouldBe(1);
        resultWithoutDisabled.Items.Any(i => i.WarehouseId == whDisabledId).ShouldBeFalse();

        resultWithDisabled.Items.Count.ShouldBe(2);
        resultWithDisabled.Items.Any(i => i.WarehouseId == whDisabledId).ShouldBeTrue();
    }

    [Fact]
    public async Task GetReportAsync_ThrowsIfCompanyNotFound()
    {
        // Arrange
        _companyRepo.FindAsync(_companyId).Returns((Company?)null);

        // Act & Assert
        await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _appService.GetReportAsync(new WarehouseWiseStockBalanceFilterDto
            {
                CompanyId = _companyId
            });
        });
    }
}
