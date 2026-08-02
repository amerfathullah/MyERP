using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Compares stock value (from Bins) against GL inventory account balances.
/// Mismatches indicate missing GL entries, orphaned stock, or posting failures.
/// Per ERPNext: Stock and Account Value Comparison report.
/// </summary>
[Authorize]
public class StockGlComparisonAppService : ApplicationService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    public StockGlComparisonAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository)
    {
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
        _accountRepository = accountRepository;
        _journalEntryRepository = journalEntryRepository;
    }

    public async Task<StockGlComparisonDto> GetComparisonAsync(StockGlComparisonRequestDto input)
    {
        var asOfDate = input.AsOfDate ?? DateTime.UtcNow.Date;

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
        var postedJournalLines = jeQuery
            .Where(je => je.CompanyId == input.CompanyId &&
                         je.Status == DocumentStatus.Posted &&
                         je.PostingDate <= asOfDate)
            .SelectMany(je => je.Lines)
            .Where(line => stockAccountIds.Contains(line.AccountId))
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
        };
    }
}
