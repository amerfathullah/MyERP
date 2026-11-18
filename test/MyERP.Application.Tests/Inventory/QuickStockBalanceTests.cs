using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

public abstract class QuickStockBalanceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IStockBalanceAppService _stockBalanceService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;

    protected QuickStockBalanceTests()
    {
        _stockBalanceService = GetRequiredService<IStockBalanceAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
    }

    [Fact]
    public async Task QuickStockBalance_Should_Aggregate_Group_Warehouse_Filter_By_Date_And_Resolve_Barcode()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Quick Balance Co"), autoSave: true);
        var groupWh = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "All Warehouses") { IsGroup = true }, autoSave: true);
        var whA = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "WH A") { ParentWarehouseId = groupWh.Id }, autoSave: true);
        var whB = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "WH B") { ParentWarehouseId = groupWh.Id }, autoSave: true);

        var item = new Item(Guid.NewGuid(), company.Id, "QSB-001", "Quick Balance Item", ItemType.Goods)
        {
            Barcode = "EAN-QSB-1",
            Description = "Scanner test item",
        };
        item.Barcodes.Add(new ItemBarcode(Guid.NewGuid(), item.Id, "EAN-QSB-ALT"));
        await _itemRepository.InsertAsync(item, autoSave: true);

        // June 1: whA = 10 @ 50 (500), whB = 5 @ 60 (300)
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, whA.Id,
            new DateTime(2026, 6, 1), 10m, 50m, 10m, 500m), autoSave: true);
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, whB.Id,
            new DateTime(2026, 6, 1), 5m, 60m, 5m, 300m), autoSave: true);

        // Future June 10 movement in whA (must be excluded when Date = June 5)
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, whA.Id,
            new DateTime(2026, 6, 10), 20m, 50m, 30m, 1500m), autoSave: true);

        var groupAsOfJune5 = await _stockBalanceService.GetQuickStockBalanceAsync(new QuickStockBalanceRequestDto
        {
            WarehouseId = groupWh.Id,
            Date = new DateTime(2026, 6, 5),
            Barcode = "EAN-QSB-ALT",
        });

        groupAsOfJune5.ItemId.ShouldBe(item.Id);
        groupAsOfJune5.MatchedBarcode.ShouldBe("EAN-QSB-ALT");
        groupAsOfJune5.Barcodes.ShouldContain("EAN-QSB-1");
        groupAsOfJune5.Barcodes.ShouldContain("EAN-QSB-ALT");
        groupAsOfJune5.Qty.ShouldBe(15m);
        groupAsOfJune5.Value.ShouldBe(800m);

        var leafAsOfJune12 = await _stockBalanceService.GetQuickStockBalanceAsync(new QuickStockBalanceRequestDto
        {
            WarehouseId = whA.Id,
            Date = new DateTime(2026, 6, 12),
            ItemId = item.Id,
        });
        leafAsOfJune12.Qty.ShouldBe(30m);
        leafAsOfJune12.Value.ShouldBe(1500m);
        leafAsOfJune12.ValuationRate.ShouldBe(50m);

        await Should.ThrowAsync<BusinessException>(async () =>
            await _stockBalanceService.GetQuickStockBalanceAsync(new QuickStockBalanceRequestDto
            {
                WarehouseId = whA.Id,
                Date = new DateTime(2026, 6, 12),
                Barcode = "NON-EXISTENT-BARCODE",
            }));
    }
}
