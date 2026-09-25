using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

public abstract class StockGlComparisonAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IStockGlComparisonAppService _comparisonAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<RepostItemValuation, Guid> _repostRepository;

    protected StockGlComparisonAppServiceTests()
    {
        _comparisonAppService = GetRequiredService<IStockGlComparisonAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        _repostRepository = GetRequiredService<IRepository<RepostItemValuation, Guid>>();
    }

    private async Task<(Guid companyId, Guid itemId, Guid warehouseId)> SeedDataAsync()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "GL Repost Test Co"), autoSave: true);
        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "GLR-ITEM-1", "GL Repost Item", ItemType.Goods), autoSave: true);
        var warehouse = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "GL Repost WH"), autoSave: true);

        return (company.Id, item.Id, warehouse.Id);
    }

    [Fact]
    public async Task CreateGlRepostingEntriesAsync_ValidVoucher_CreatesQueuedGlOnlyRepost()
    {
        var (companyId, itemId, warehouseId) = await SeedDataAsync();
        var voucherId = Guid.NewGuid();
        var postingDate = DateTime.Today;

        var sle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId, postingDate, 10, 15, 10, 150)
        {
            VoucherType = "Stock Entry",
            VoucherId = voucherId,
            PostingDateTime = postingDate.AddHours(10)
        };
        await _sleRepository.InsertAsync(sle, autoSave: true);

        var result = await _comparisonAppService.CreateGlRepostingEntriesAsync(new CreateGlRepostingInputDto
        {
            CompanyId = companyId,
            FromDate = postingDate.AddDays(-1),
            Vouchers = new()
            {
                new() { VoucherType = "Stock Entry", VoucherId = voucherId }
            }
        });

        result.CreatedCount.ShouldBe(1);
        result.SkippedCount.ShouldBe(0);
        result.RepostIds.Count.ShouldBe(1);

        var repost = await _repostRepository.GetAsync(result.RepostIds[0]);
        repost.ShouldNotBeNull();
        repost.BasedOn.ShouldBe(RepostMethod.Transaction);
        repost.RepostOnlyAccountingLedgers.ShouldBeTrue();
        repost.VoucherType.ShouldBe("Stock Entry");
        repost.VoucherId.ShouldBe(voucherId);
        repost.Status.ShouldBe(RepostStatus.Queued);
    }

    [Fact]
    public async Task CreateGlRepostingEntriesAsync_VoucherBeforeFromDate_IsSkipped()
    {
        var (companyId, itemId, warehouseId) = await SeedDataAsync();
        var voucherId = Guid.NewGuid();
        var postingDate = DateTime.Today.AddDays(-10);

        var sle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId, postingDate, 5, 20, 5, 100)
        {
            VoucherType = "Stock Entry",
            VoucherId = voucherId,
            PostingDateTime = postingDate.AddHours(8)
        };
        await _sleRepository.InsertAsync(sle, autoSave: true);

        var result = await _comparisonAppService.CreateGlRepostingEntriesAsync(new CreateGlRepostingInputDto
        {
            CompanyId = companyId,
            FromDate = DateTime.Today.AddDays(-2),
            Vouchers = new()
            {
                new() { VoucherType = "Stock Entry", VoucherId = voucherId }
            }
        });

        result.CreatedCount.ShouldBe(0);
        result.SkippedCount.ShouldBe(1);
        result.RepostIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateGlRepostingEntriesAsync_VoucherWithoutStock_IsSkipped()
    {
        var (companyId, _, _) = await SeedDataAsync();
        var dummyVoucherId = Guid.NewGuid();

        var result = await _comparisonAppService.CreateGlRepostingEntriesAsync(new CreateGlRepostingInputDto
        {
            CompanyId = companyId,
            FromDate = DateTime.Today.AddDays(-30),
            Vouchers = new()
            {
                new() { VoucherType = "Purchase Invoice", VoucherId = dummyVoucherId }
            }
        });

        result.CreatedCount.ShouldBe(0);
        result.SkippedCount.ShouldBe(1);
    }
}
