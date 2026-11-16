using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.BackgroundJobs;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Compares stock value (from Bins) against GL inventory account balances.
/// Mismatches indicate missing GL entries, orphaned stock, or posting failures.
/// Per ERPNext: Stock and Account Value Comparison report.
/// </summary>
[Authorize]
public class StockGlComparisonAppService : ApplicationService, IStockGlComparisonAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<StockLedgerEntry, Guid>? _stockLedgerEntryRepository;

    public StockGlComparisonAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<StockLedgerEntry, Guid>? stockLedgerEntryRepository = null)
    {
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
        _accountRepository = accountRepository;
        _journalEntryRepository = journalEntryRepository;
        _stockLedgerEntryRepository = stockLedgerEntryRepository;
    }

    public async Task<StockGlComparisonDto> GetComparisonAsync(StockGlComparisonRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;
        var asOfEnd = asOfDate.Date.AddDays(1).AddTicks(-1);

        // 1. Stock value from Bins (ActualQty × ValuationRate per warehouse)
        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery
            .Where(b => b.ActualQty > 0)
            .ToList();

        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        var companyWarehouses = warehouseQuery
            .Where(w => w.CompanyId == input.CompanyId && !w.IsGroup)
            .ToList();

        var companyWarehouseIds = companyWarehouses.Select(w => w.Id).ToHashSet();
        var relevantBins = bins.Where(b => companyWarehouseIds.Contains(b.WarehouseId)).ToList();

        // Group by warehouse for per-warehouse comparison
        var stockByWarehouse = relevantBins
            .GroupBy(b => b.WarehouseId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(b => b.ActualQty * b.ValuationRate));

        var totalStockValue = stockByWarehouse.Values.Sum();

        // 2. GL balance from Stock-type accounts (posted JE lines with Debit - Credit)
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var stockAccounts = accountQuery
            .Where(a => a.CompanyId == input.CompanyId && a.AccountSubType == AccountSubType.Stock)
            .ToList();

        var stockAccountIds = stockAccounts.Select(a => a.Id).ToHashSet();
        var stockAccountMap = stockAccounts.ToDictionary(a => a.Id, a => $"{a.AccountCode} {a.AccountName}");

        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var postedJournalEntries = jeQuery
            .Where(je => je.CompanyId == input.CompanyId &&
                         je.Status == DocumentStatus.Posted &&
                         je.PostingDate <= asOfEnd)
            .Select(je => new
            {
                je.Id,
                je.ReferenceType,
                je.ReferenceId,
                je.ReferenceNumber,
                je.EntryNumber,
                je.PostingDate,
                StockLines = je.Lines
                    .Where(l => stockAccountIds.Contains(l.AccountId))
                    .Select(l => new { l.AccountId, l.IsDebit, l.Amount })
                    .ToList()
            })
            .Where(je => je.StockLines.Any())
            .ToList();

        var postedJournalLines = postedJournalEntries
            .SelectMany(je => je.StockLines)
            .ToList();

        var totalGlBalance = postedJournalLines.Sum(l => l.IsDebit ? l.Amount : -l.Amount);

        // 3. Per-warehouse GL balance (when warehouse-specific stock accounts exist)
        var warehouseNames = companyWarehouses.ToDictionary(w => w.Id, w => w.Name);
        var warehouseAccountMap = companyWarehouses
            .Where(w => w.DefaultAccountId.HasValue)
            .ToDictionary(w => w.Id, w => w.DefaultAccountId!.Value);

        var perWarehouse = new List<StockGlWarehouseComparisonDto>();
        foreach (var wh in companyWarehouses.Where(w => stockByWarehouse.ContainsKey(w.Id)))
        {
            var whStockValue = stockByWarehouse.GetValueOrDefault(wh.Id);
            decimal whGlBalance = 0;

            if (wh.DefaultAccountId.HasValue && stockAccountIds.Contains(wh.DefaultAccountId.Value))
            {
                whGlBalance = postedJournalLines
                    .Where(l => l.AccountId == wh.DefaultAccountId.Value)
                    .Sum(l => l.IsDebit ? l.Amount : -l.Amount);
            }

            var diff = whStockValue - whGlBalance;
            perWarehouse.Add(new StockGlWarehouseComparisonDto
            {
                WarehouseId = wh.Id,
                WarehouseName = warehouseNames.GetValueOrDefault(wh.Id, "—"),
                StockValue = whStockValue,
                GlBalance = whGlBalance,
                Difference = diff,
                HasMismatch = Math.Abs(diff) > 0.01m,
                StockAccountId = wh.DefaultAccountId,
                StockAccountName = wh.DefaultAccountId.HasValue
                    ? stockAccountMap.GetValueOrDefault(wh.DefaultAccountId.Value)
                    : null,
            });
        }

        // 4. Voucher-level comparison (SLE vs GL on Stock accounts, per ERPNext stock_and_account_value_comparison)
        var sleRepo = _stockLedgerEntryRepository ?? LazyServiceProvider?.LazyGetService<IRepository<StockLedgerEntry, Guid>>();
        var mismatchedVouchers = new List<StockGlVoucherComparisonDto>();
        if (sleRepo != null)
        {
            var sleQuery = await sleRepo.GetQueryableAsync();
            var sleGroups = sleQuery
                .Where(s => s.CompanyId == input.CompanyId &&
                            !s.IsCancelled &&
                            s.PostingDate <= asOfEnd &&
                            s.VoucherId.HasValue &&
                            !string.IsNullOrEmpty(s.VoucherType))
                .GroupBy(s => new { s.VoucherType, VoucherId = s.VoucherId!.Value })
                .Select(g => new
                {
                    VoucherType = g.Key.VoucherType!,
                    g.Key.VoucherId,
                    StockValue = g.Sum(s => s.StockValueDifference != 0 ? s.StockValueDifference : s.QuantityChange * s.ValuationRate),
                    PostingDate = g.Max(s => s.PostingDate)
                })
                .ToList();

            var glByVoucher = postedJournalEntries
                .GroupBy(je => (
                    NormalizedType: NormalizeVoucherType(string.IsNullOrEmpty(je.ReferenceType) ? "JournalEntry" : je.ReferenceType),
                    VoucherId: je.ReferenceId ?? je.Id))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        VoucherType = g.Select(x => string.IsNullOrEmpty(x.ReferenceType) ? "JournalEntry" : x.ReferenceType!).First(),
                        VoucherId = g.Key.VoucherId,
                        VoucherNumber = g.Select(x => x.ReferenceNumber ?? x.EntryNumber).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                        PostingDate = g.Max(x => x.PostingDate),
                        GlBalance = g.SelectMany(x => x.StockLines).Sum(l => l.IsDebit ? l.Amount : -l.Amount)
                    });

            foreach (var sle in sleGroups)
            {
                var key = (NormalizeVoucherType(sle.VoucherType), sle.VoucherId);
                glByVoucher.TryGetValue(key, out var glEntry);
                var glBalance = glEntry?.GlBalance ?? 0m;
                var diff = sle.StockValue - glBalance;

                if (Math.Abs(diff) > 0.1m)
                {
                    mismatchedVouchers.Add(new StockGlVoucherComparisonDto
                    {
                        LedgerType = "Stock Ledger Entry",
                        VoucherType = sle.VoucherType,
                        VoucherId = sle.VoucherId,
                        VoucherNumber = glEntry?.VoucherNumber,
                        PostingDate = sle.PostingDate,
                        StockValue = sle.StockValue,
                        GlBalance = glBalance,
                        Difference = diff,
                    });
                }

                if (glEntry != null)
                {
                    glByVoucher.Remove(key);
                }
            }

            foreach (var orphanGl in glByVoucher.Values)
            {
                if (Math.Abs(orphanGl.GlBalance) > 0.1m)
                {
                    mismatchedVouchers.Add(new StockGlVoucherComparisonDto
                    {
                        LedgerType = "GL Entry",
                        VoucherType = orphanGl.VoucherType,
                        VoucherId = orphanGl.VoucherId,
                        VoucherNumber = orphanGl.VoucherNumber,
                        PostingDate = orphanGl.PostingDate,
                        StockValue = 0m,
                        GlBalance = orphanGl.GlBalance,
                        Difference = -orphanGl.GlBalance,
                    });
                }
            }
        }

        var totalDifference = totalStockValue - totalGlBalance;

        return new StockGlComparisonDto
        {
            TotalStockValue = totalStockValue,
            TotalGlBalance = totalGlBalance,
            Difference = totalDifference,
            IsMatched = Math.Abs(totalDifference) <= 0.01m,
            WarehouseCount = perWarehouse.Count,
            ItemCount = relevantBins.Select(b => b.ItemId).Distinct().Count(),
            AsOfDate = asOfDate,
            PerWarehouse = perWarehouse.OrderByDescending(w => Math.Abs(w.Difference)).ToList(),
            MismatchedVouchers = mismatchedVouchers
                .OrderBy(v => v.PostingDate)
                .ThenBy(v => v.VoucherType)
                .ThenBy(v => v.VoucherId)
                .ToList(),
        };
    }

    private static string NormalizeVoucherType(string? voucherType) =>
        (voucherType ?? string.Empty).Replace(" ", string.Empty).Trim();

    /// <summary>
    /// Repost only the accounting ledgers for the selected vouchers posted on or after FromDate.
    /// Per ERPNext PR #59307 / commit 18093079d9: Stock and Account Value Comparison GL-only repost.
    /// Leaves stock ledgers and valuation rates untouched, only rebuilding GL entries.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<CreateGlRepostingResultDto> CreateGlRepostingEntriesAsync(CreateGlRepostingInputDto input)
    {
        if (input.FromDate == default)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select the date to repost the accounting ledgers from.");
        }

        if (input.Vouchers == null || !input.Vouchers.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select rows to create GL Reposting Entries.");
        }

        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var repostRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<RepostItemValuation, Guid>>();
        var jobManager = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.BackgroundJobs.IBackgroundJobManager>();

        var voucherIds = input.Vouchers.Select(v => v.VoucherId).Distinct().ToList();

        // 1. Fetch earliest active SLE posting date/time for each voucher in company
        var sleQuery = await sleRepo.GetQueryableAsync();
        var voucherPostings = sleQuery
            .Where(s => s.CompanyId == input.CompanyId && !s.IsCancelled && s.VoucherId.HasValue && voucherIds.Contains(s.VoucherId.Value))
            .GroupBy(s => new { s.VoucherType, VoucherId = s.VoucherId!.Value })
            .Select(g => new
            {
                g.Key.VoucherType,
                g.Key.VoucherId,
                MinDate = g.Min(s => s.PostingDate),
                MinTime = g.Min(s => s.PostingDateTime)
            })
            .ToList()
            .ToDictionary(k => (k.VoucherType, k.VoucherId));

        // 2. Fetch already queued or in-progress GL-only reposts for these vouchers
        var repostQuery = await repostRepo.GetQueryableAsync();
        var pendingVouchers = repostQuery
            .Where(r => r.CompanyId == input.CompanyId &&
                        r.BasedOn == RepostMethod.Transaction &&
                        r.RepostOnlyAccountingLedgers &&
                        (r.Status == RepostStatus.Queued || r.Status == RepostStatus.InProgress) &&
                        r.VoucherId.HasValue && voucherIds.Contains(r.VoucherId.Value))
            .Select(r => new { r.VoucherType, VoucherId = r.VoucherId!.Value })
            .ToList()
            .Select(r => (r.VoucherType, r.VoucherId))
            .ToHashSet();

        var createdIds = new List<Guid>();
        var processedVouchers = new HashSet<(string, Guid)>();
        int skippedCount = 0;

        var fromDate = input.FromDate.Date;

        foreach (var voucher in input.Vouchers)
        {
            if (string.IsNullOrEmpty(voucher.VoucherType))
            {
                skippedCount++;
                continue;
            }

            var key = (voucher.VoucherType, voucher.VoucherId);
            if (processedVouchers.Contains(key))
            {
                skippedCount++;
                continue;
            }
            processedVouchers.Add(key);

            // Skip vouchers without stock ledger entries (e.g. GL entries or journals without stock)
            if (!voucherPostings.TryGetValue(key, out var posting))
            {
                skippedCount++;
                continue;
            }

            // Skip vouchers posted before FromDate
            if (posting.MinDate.Date < fromDate)
            {
                skippedCount++;
                continue;
            }

            // Skip vouchers that already have a queued/in-progress GL-only repost
            if (pendingVouchers.Contains(key))
            {
                skippedCount++;
                continue;
            }

            var entity = new RepostItemValuation(
                GuidGenerator.Create(),
                input.CompanyId,
                RepostMethod.Transaction,
                posting.MinDate,
                tenantId: CurrentTenant.Id)
            {
                PostingTime = posting.MinTime.TimeOfDay,
                VoucherType = voucher.VoucherType,
                VoucherId = voucher.VoucherId,
                RepostOnlyAccountingLedgers = true,
                RepostGlEntries = true
            };

            entity.ResetRepostOnlyAccountingLedgers();
            entity.ValidateRepostOnlyAccountingLedgers();

            await repostRepo.InsertAsync(entity);
            createdIds.Add(entity.Id);

            await jobManager.EnqueueAsync(new RepostItemValuationArgs
            {
                RepostId = entity.Id,
                CompanyId = entity.CompanyId,
                FromDate = entity.PostingDate,
                TenantId = entity.TenantId,
                Reason = "GL-only repost from Stock and Account Value Comparison"
            });
        }

        return new CreateGlRepostingResultDto
        {
            CreatedCount = createdIds.Count,
            SkippedCount = skippedCount,
            RepostIds = createdIds,
            Message = createdIds.Any()
                ? $"GL reposting entries created: {createdIds.Count}"
                : "No new GL reposting entries were created for the selected rows."
        };
    }
}
