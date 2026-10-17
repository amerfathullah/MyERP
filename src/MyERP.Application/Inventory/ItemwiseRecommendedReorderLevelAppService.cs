using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class ItemwiseRecommendedReorderLevelAppService : ApplicationService, IItemwiseRecommendedReorderLevelAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public ItemwiseRecommendedReorderLevelAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _sleRepository = sleRepository;
        _stockEntryRepository = stockEntryRepository;
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
    }

    public async Task<ItemwiseRecommendedReorderLevelReportDto> GetReportAsync(ItemwiseRecommendedReorderLevelFilterDto input)
    {
        if (input.FromDate > input.ToDate)
        {
            throw new UserFriendlyException("From date cannot be after To date.");
        }

        var company = await _companyRepository.FindAsync(input.CompanyId);
        if (company == null)
        {
            throw new UserFriendlyException("Company not found.");
        }

        var totalDays = Math.Max(1, (input.ToDate.Date - input.FromDate.Date).Days + 1);

        // Fetch all active stock items for company
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.CompanyId == input.CompanyId && i.MaintainStock);

        if (input.ItemGroupId.HasValue && input.ItemGroupId != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Brand))
        {
            itemQuery = itemQuery.Where(i => i.Brand == input.Brand);
        }

        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            var search = input.SearchText.Trim().ToLower();
            itemQuery = itemQuery.Where(i =>
                i.ItemCode.ToLower().Contains(search) ||
                i.ItemName.ToLower().Contains(search) ||
                (i.Description != null && i.Description.ToLower().Contains(search)));
        }

        var items = itemQuery
            .Select(i => new
            {
                i.Id,
                i.ItemCode,
                i.ItemName,
                i.ItemGroup,
                i.Brand,
                i.Description,
                i.SafetyStock,
                i.LeadTimeDays,
                i.ReorderLevel
            })
            .ToList();

        if (items.Count == 0)
        {
            return new ItemwiseRecommendedReorderLevelReportDto();
        }

        var itemIds = items.Select(i => i.Id).ToList();

        // Query outgoing SLEs within date range
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var sles = sleQuery
            .Where(s =>
                s.CompanyId == input.CompanyId &&
                s.PostingDate >= input.FromDate.Date &&
                s.PostingDate <= input.ToDate.Date &&
                !s.IsCancelled &&
                !s.IsAdjustmentEntry &&
                s.QuantityChange < 0 &&
                itemIds.Contains(s.ItemId))
            .Select(s => new
            {
                s.ItemId,
                s.QuantityChange,
                s.VoucherType,
                s.VoucherId
            })
            .ToList();

        // Get StockEntries to exclude internal warehouse transfers
        var stockEntryIds = sles
            .Where(s => s.VoucherType == "StockEntry" && s.VoucherId.HasValue)
            .Select(s => s.VoucherId!.Value)
            .Distinct()
            .ToList();

        var transferStockEntryIds = new HashSet<Guid>();
        if (stockEntryIds.Count > 0)
        {
            var seQuery = await _stockEntryRepository.GetQueryableAsync();
            transferStockEntryIds = seQuery
                .Where(se => stockEntryIds.Contains(se.Id) && se.EntryType == StockEntryType.MaterialTransfer)
                .Select(se => se.Id)
                .ToHashSet();
        }

        var consumedMap = new Dictionary<Guid, decimal>();
        var deliveredMap = new Dictionary<Guid, decimal>();

        foreach (var s in sles)
        {
            var qty = Math.Abs(s.QuantityChange);

            if (s.VoucherType == "DeliveryNote" || s.VoucherType == "SalesInvoice")
            {
                deliveredMap[s.ItemId] = deliveredMap.GetValueOrDefault(s.ItemId) + qty;
            }
            else if (s.VoucherType == "StockEntry")
            {
                if (s.VoucherId.HasValue && transferStockEntryIds.Contains(s.VoucherId.Value))
                {
                    continue; // Skip inter-warehouse transfer
                }
                consumedMap[s.ItemId] = consumedMap.GetValueOrDefault(s.ItemId) + qty;
            }
            else
            {
                consumedMap[s.ItemId] = consumedMap.GetValueOrDefault(s.ItemId) + qty;
            }
        }

        var rows = new List<ItemwiseRecommendedReorderLevelRowDto>();

        foreach (var item in items)
        {
            var consumed = consumedMap.GetValueOrDefault(item.Id, 0m);
            var delivered = deliveredMap.GetValueOrDefault(item.Id, 0m);
            var totalOutgoing = consumed + delivered;
            var avgDaily = totalOutgoing / totalDays;
            var recommended = Math.Round((avgDaily * item.LeadTimeDays) + item.SafetyStock, 2);
            var variance = Math.Round(recommended - item.ReorderLevel, 2);

            string status;
            if (variance > 0.05m * (item.ReorderLevel > 0 ? item.ReorderLevel : 1))
            {
                status = "UnderConfigured";
            }
            else if (variance < -0.05m * (item.ReorderLevel > 0 ? item.ReorderLevel : 1))
            {
                status = "OverConfigured";
            }
            else
            {
                status = "Adequate";
            }

            rows.Add(new ItemwiseRecommendedReorderLevelRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemGroup = item.ItemGroup,
                Brand = item.Brand,
                Description = item.Description,
                SafetyStock = item.SafetyStock,
                LeadTimeDays = item.LeadTimeDays,
                ConsumedQty = Math.Round(consumed, 2),
                DeliveredQty = Math.Round(delivered, 2),
                TotalOutgoing = Math.Round(totalOutgoing, 2),
                AvgDailyOutgoing = Math.Round(avgDaily, 4),
                CurrentReorderLevel = item.ReorderLevel,
                RecommendedReorderLevel = recommended,
                Variance = variance,
                Status = status
            });
        }

        var sortedRows = rows
            .OrderByDescending(r => r.Variance > 0)
            .ThenByDescending(r => Math.Abs(r.Variance))
            .ToList();

        return new ItemwiseRecommendedReorderLevelReportDto
        {
            Rows = sortedRows,
            TotalItems = sortedRows.Count,
            UnderConfiguredCount = sortedRows.Count(r => r.Status == "UnderConfigured"),
            OverConfiguredCount = sortedRows.Count(r => r.Status == "OverConfigured"),
            TotalAvgDailyOutgoing = Math.Round(sortedRows.Sum(r => r.AvgDailyOutgoing), 4)
        };
    }
}
