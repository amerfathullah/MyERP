using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
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
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    protected StockGlComparisonAppServiceTests()
    {
        _comparisonAppService = GetRequiredService<IStockGlComparisonAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        _repostRepository = GetRequiredService<IRepository<RepostItemValuation, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
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

    [Fact]
    public async Task GetComparisonAsync_DetectsMismatchedAndMatchedVouchers()
    {
        var (companyId, itemId, warehouseId) = await SeedDataAsync();
        var postingDate = DateTime.Today;
        var fiscalYearId = Guid.NewGuid();

        var stockAccount = new Account(Guid.NewGuid(), companyId, "1400", "Stock In Hand", AccountType.Asset)
        {
            AccountSubType = AccountSubType.Stock
        };
        var adjustmentAccount = new Account(Guid.NewGuid(), companyId, "5100", "Stock Adjustment", AccountType.Expense);
        await _accountRepository.InsertAsync(stockAccount, autoSave: true);
        await _accountRepository.InsertAsync(adjustmentAccount, autoSave: true);

        // Matched voucher: SLE 150 == JE 150
        var matchedVoucherId = Guid.NewGuid();
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId, postingDate, 10, 15, 10, 150)
        {
            VoucherType = "StockEntry",
            VoucherId = matchedVoucherId,
            PostingDateTime = postingDate.AddHours(9)
        }, autoSave: true);

        var matchedJe = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, postingDate)
        {
            EntryNumber = "JE-MATCH",
            ReferenceType = "StockEntry",
            ReferenceId = matchedVoucherId,
            ReferenceNumber = "SE-0001"
        };
        matchedJe.AddLine(stockAccount.Id, 150m, true);
        matchedJe.AddLine(adjustmentAccount.Id, 150m, false);
        matchedJe.Post();
        await _journalEntryRepository.InsertAsync(matchedJe, autoSave: true);

        // Mismatched voucher: SLE 200 vs JE 120 (diff 80)
        var mismatchedVoucherId = Guid.NewGuid();
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId, postingDate, 10, 20, 20, 350)
        {
            VoucherType = "StockEntry",
            VoucherId = mismatchedVoucherId,
            PostingDateTime = postingDate.AddHours(10)
        }, autoSave: true);

        var mismatchedJe = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, postingDate)
        {
            EntryNumber = "JE-MISMATCH",
            ReferenceType = "StockEntry",
            ReferenceId = mismatchedVoucherId,
            ReferenceNumber = "SE-0002"
        };
        mismatchedJe.AddLine(stockAccount.Id, 120m, true);
        mismatchedJe.AddLine(adjustmentAccount.Id, 120m, false);
        mismatchedJe.Post();
        await _journalEntryRepository.InsertAsync(mismatchedJe, autoSave: true);

        // SLE without any GL entry: SLE 75 vs GL 0 (diff 75)
        var unpostedVoucherId = Guid.NewGuid();
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId, postingDate, 5, 15, 25, 425)
        {
            VoucherType = "DeliveryNote",
            VoucherId = unpostedVoucherId,
            PostingDateTime = postingDate.AddHours(11)
        }, autoSave: true);

        var comparison = await _comparisonAppService.GetComparisonAsync(new StockGlComparisonRequestDto
        {
            CompanyId = companyId,
            AsOfDate = postingDate
        });

        comparison.MismatchedVouchers.Count.ShouldBe(2);
        comparison.MismatchedVouchers.ShouldNotContain(v => v.VoucherId == matchedVoucherId);

        var partialMismatch = comparison.MismatchedVouchers.Single(v => v.VoucherId == mismatchedVoucherId);
        partialMismatch.LedgerType.ShouldBe("Stock Ledger Entry");
        partialMismatch.VoucherNumber.ShouldBe("SE-0002");
        partialMismatch.StockValue.ShouldBe(200m);
        partialMismatch.GlBalance.ShouldBe(120m);
        partialMismatch.Difference.ShouldBe(80m);

        var missingGl = comparison.MismatchedVouchers.Single(v => v.VoucherId == unpostedVoucherId);
        missingGl.LedgerType.ShouldBe("Stock Ledger Entry");
        missingGl.VoucherType.ShouldBe("DeliveryNote");
        missingGl.StockValue.ShouldBe(75m);
        missingGl.GlBalance.ShouldBe(0m);
        missingGl.Difference.ShouldBe(75m);
    }
}
