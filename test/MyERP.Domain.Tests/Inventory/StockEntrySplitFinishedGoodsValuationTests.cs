using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Unit tests verifying ERPNext PR #59881 (commit 37f16db9d8):
/// Split manufacture/repack cost across all finished good rows,
/// ignore manual and zero-valued rows in the denominator,
/// and ensure TotalIncomingValue == TotalOutgoingValue.
/// </summary>
public class StockEntrySplitFinishedGoodsValuationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _sourceWhId = Guid.NewGuid();
    private readonly Guid _targetWh1Id = Guid.NewGuid();
    private readonly Guid _targetWh2Id = Guid.NewGuid();

    private StockEntryManager CreateManager()
    {
        return new StockEntryManager(null!, null!, null!);
    }

    [Fact]
    public void StockEntry_GetFinishedItemsQty_Calculates_Only_Eligible_FG_Rows()
    {
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);

        // Raw material (source warehouse, no target warehouse) -> not FG
        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);

        // Eligible FG row 1
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true);

        // Eligible FG row 2
        entry.AddItem(_fgItemId, 3m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        // Zero-valued FG row -> excluded
        entry.AddItem(_fgItemId, 2m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, allowZeroValuationRate: true);

        // Manual rate FG row -> excluded
        entry.AddItem(_fgItemId, 4m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true, setBasicRateManually: true, valuationRate: 80m);

        entry.GetFinishedItemsQty().ShouldBe(8m); // 5 + 3
    }

    [Fact]
    public void CalculateManufactureFgRate_Splits_Cost_Over_All_Eligible_FG_Rows()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);

        // RM: 10 units @ 100 = 1000 total cost
        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);

        // FG split across 2 target warehouses: 5 units each (total 10 units)
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true);
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        var rate = manager.CalculateManufactureFgRate(entry.Items);

        // 1000 / (5 + 5) = 100 per unit
        rate.ShouldBe(100m);
    }

    [Fact]
    public void CalculateManufactureFgRate_Zero_Valued_FG_Row_Takes_No_Share()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);

        // RM: 10 units @ 100 = 1000 total cost
        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);

        // FG 1: 5 units with AllowZeroValuationRate = true
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, allowZeroValuationRate: true);

        // FG 2: 5 units normal
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        entry.GetFinishedItemsQty().ShouldBe(5m);

        var rate = manager.CalculateManufactureFgRate(entry.Items);

        // The non-zero row carries the entire 1000 cost: 1000 / 5 = 200
        rate.ShouldBe(200m);
    }

    [Fact]
    public void CalculateManufactureFgRate_All_Zero_Valued_FG_Rows_Yields_Zero()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);

        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, allowZeroValuationRate: true);
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true, allowZeroValuationRate: true);

        entry.GetFinishedItemsQty().ShouldBe(0m);

        var rate = manager.CalculateManufactureFgRate(entry.Items);
        rate.ShouldBe(0m);
    }

    [Fact]
    public void CalculateRepackFgRate_Splits_Cost_And_Deducts_Manual_Rate_Items()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow);

        // Outgoing RM: 10 units @ 100 = 1000 total outgoing cost
        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);

        // FG 1: 5 units with manual rate 50 (value 250)
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, setBasicRateManually: true, valuationRate: 50m);

        // FG 2: 5 units with derived rate
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        var rate = manager.CalculateRepackFgRate(entry.Items);

        // (1000 - 250) / 5 = 150
        rate.ShouldBe(150m);
    }

    [Fact]
    public async Task StockPostingService_PostStockEntry_Manufacture_Splits_Cost_Across_Multiple_FG_Warehouses()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var company = new Company(_companyId, "Test Company");
        companyRepo.GetAsync(_companyId).Returns(company);
        companyRepo.FindAsync(_companyId).Returns(company);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var rmItem = new Item(_rmItemId, _companyId, "RM-01", "Raw Material 1", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true };
        var fgItem = new Item(_fgItemId, _companyId, "FG-01", "Finished Good 1", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true };
        itemRepo.FindAsync(_rmItemId).Returns(rmItem);
        itemRepo.FindAsync(_fgItemId).Returns(fgItem);
        itemRepo.GetAsync(_rmItemId).Returns(rmItem);
        itemRepo.GetAsync(_fgItemId).Returns(fgItem);

        var binService = new BinService(binRepo);
        var valService = new StockValuationService(sleRepo, itemRepo, settingProvider);
        var valSp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        valSp.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(Volo.Abp.Guids.SimpleGuidGenerator.Instance);
        typeof(Volo.Abp.Domain.Services.DomainService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(valService, valSp);

        var postingService = new StockPostingService(
            sleRepo, companyRepo, itemRepo, whRepo, binService, valService);

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        // RM: 10 @ 100 = 1000
        se.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);
        // Split FG into two warehouses: 5 units in WH1, 5 units in WH2
        var fg1 = se.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true);
        var fg2 = se.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        await postingService.PostStockEntryAsync(se);

        // Each FG row rate must be 100, not 200
        fg1.ValuationRate.ShouldBe(100m);
        fg2.ValuationRate.ShouldBe(100m);

        // Total incoming == Total outgoing (1000 == 1000)
        se.TotalIncomingValue.ShouldBe(1000m);
        se.TotalOutgoingValue.ShouldBe(1000m);
        se.TotalValueDifference.ShouldBe(0m);
    }

    [Fact]
    public async Task StockPostingService_PostStockEntry_Manufacture_Gives_Zero_Valued_Row_No_Cost()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var company = new Company(_companyId, "Test Company");
        companyRepo.GetAsync(_companyId).Returns(company);
        companyRepo.FindAsync(_companyId).Returns(company);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var rmItem = new Item(_rmItemId, _companyId, "RM-01", "Raw Material 1", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true };
        var fgItem = new Item(_fgItemId, _companyId, "FG-01", "Finished Good 1", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true };
        itemRepo.FindAsync(_rmItemId).Returns(rmItem);
        itemRepo.FindAsync(_fgItemId).Returns(fgItem);
        itemRepo.GetAsync(_rmItemId).Returns(rmItem);
        itemRepo.GetAsync(_fgItemId).Returns(fgItem);

        var binService = new BinService(binRepo);
        var valService = new StockValuationService(sleRepo, itemRepo, settingProvider);
        var valSp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        valSp.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(Volo.Abp.Guids.SimpleGuidGenerator.Instance);
        typeof(Volo.Abp.Domain.Services.DomainService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(valService, valSp);

        var postingService = new StockPostingService(
            sleRepo, companyRepo, itemRepo, whRepo, binService, valService);

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        // RM: 10 @ 100 = 1000
        se.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);
        // FG 1: 5 units with AllowZeroValuationRate = true
        var fg1 = se.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, allowZeroValuationRate: true);
        // FG 2: 5 units normal
        var fg2 = se.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        await postingService.PostStockEntryAsync(se);

        // FG 1 must have 0 rate
        fg1.ValuationRate.ShouldBe(0m);
        // FG 2 carries the entire 1000 cost over its 5 units -> 200 per unit
        fg2.ValuationRate.ShouldBe(200m);

        // Total incoming == Total outgoing (1000 == 1000)
        se.TotalIncomingValue.ShouldBe(1000m);
        se.TotalOutgoingValue.ShouldBe(1000m);
        se.TotalValueDifference.ShouldBe(0m);
    }

    [Fact]
    public void CalculateManufactureFgRate_Deducts_Manually_Rated_FG_Row_Value()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);

        // RM: 10 units @ 100 = 1000 total outgoing cost
        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);

        // FG 1: 5 units with manual rate 50 (value 250)
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, setBasicRateManually: true, valuationRate: 50m);

        // FG 2: 5 units with derived rate
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        var rate = manager.CalculateManufactureFgRate(entry.Items);

        // (1000 - 250) / 5 = 150
        rate.ShouldBe(150m);
    }

    [Fact]
    public void ValidateManufactureItems_ManualCostExceedsConsumedCost_Throws()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);

        // RM: 10 units @ 100 = 1000 total outgoing cost
        entry.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);

        // FG 1: 5 units with manual rate 300 = 1500 (exceeds 1000)
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, setBasicRateManually: true, valuationRate: 300m);

        // FG 2: 5 units normal (derived)
        entry.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        var ex = Should.Throw<Volo.Abp.BusinessException>(() => manager.ValidateManufactureItems(entry));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task StockPostingService_PostStockEntry_Manufacture_Takes_Manually_Rated_Finished_Good_Value_Out_Of_Cost()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var company = new Company(_companyId, "Test Company");
        companyRepo.GetAsync(_companyId).Returns(company);
        companyRepo.FindAsync(_companyId).Returns(company);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var rmItem = new Item(_rmItemId, _companyId, "RM-01", "Raw Material 1", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true };
        var fgItem = new Item(_fgItemId, _companyId, "FG-01", "Finished Good 1", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true };
        itemRepo.FindAsync(_rmItemId).Returns(rmItem);
        itemRepo.FindAsync(_fgItemId).Returns(fgItem);
        itemRepo.GetAsync(_rmItemId).Returns(rmItem);
        itemRepo.GetAsync(_fgItemId).Returns(fgItem);

        var binService = new BinService(binRepo);
        var valService = new StockValuationService(sleRepo, itemRepo, settingProvider);
        var valSp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        valSp.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(Volo.Abp.Guids.SimpleGuidGenerator.Instance);
        typeof(Volo.Abp.Domain.Services.DomainService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(valService, valSp);

        var postingService = new StockPostingService(
            sleRepo, companyRepo, itemRepo, whRepo, binService, valService);

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        // RM: 10 @ 100 = 1000
        se.AddItem(_rmItemId, 10m, sourceWarehouseId: _sourceWhId, targetWarehouseId: null, valuationRate: 100m);
        // FG 1: 5 units with manual rate 50
        var fg1 = se.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh1Id, isFinishedItem: true, setBasicRateManually: true, valuationRate: 50m);
        // FG 2: 5 units normal (derived)
        var fg2 = se.AddItem(_fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: _targetWh2Id, isFinishedItem: true);

        await postingService.PostStockEntryAsync(se);

        // FG 1 keeps manual rate 50
        fg1.ValuationRate.ShouldBe(50m);
        // FG 2 gets derived rate: (1000 - 250) / 5 = 150
        fg2.ValuationRate.ShouldBe(150m);

        // Total incoming == Total outgoing (1000 == 1000)
        se.TotalIncomingValue.ShouldBe(1000m);
        se.TotalOutgoingValue.ShouldBe(1000m);
        se.TotalValueDifference.ShouldBe(0m);
    }
}
