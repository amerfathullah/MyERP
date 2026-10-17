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

[Authorize(MyERPPermissions.StockEntries.Default)]
public class CogsByItemGroupAppService : ApplicationService, ICogsByItemGroupAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public CogsByItemGroupAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
        _companyRepository = companyRepository;
    }

    public async Task<CogsByItemGroupReportDto> GetReportAsync(CogsByItemGroupFilterDto input)
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

        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s =>
            s.CompanyId == input.CompanyId &&
            s.PostingDate >= input.FromDate &&
            s.PostingDate <= input.ToDate &&
            !s.IsCancelled &&
            s.QuantityChange < 0 &&
            s.StockValueDifference < 0);

        if (input.WarehouseId.HasValue && input.WarehouseId != Guid.Empty)
        {
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);
        }

        var outgoingSles = sleQuery
            .Select(s => new { s.ItemId, s.StockValueDifference })
            .ToList();

        if (outgoingSles.Count == 0)
        {
            return new CogsByItemGroupReportDto();
        }

        var cogsByItemId = outgoingSles
            .GroupBy(s => s.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.StockValueDifference)));

        var itemIds = cogsByItemId.Keys.ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemGroupId, i.ItemGroup })
            .ToList();

        var itemGroups = await _itemGroupRepository.GetListAsync();
        var groupById = itemGroups.ToDictionary(g => g.Id);
        var groupByName = itemGroups
            .GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Node for tree calculation
        var nodes = itemGroups.ToDictionary(g => g.Id, g => new ItemGroupTreeNode
        {
            Id = g.Id,
            Name = g.Name,
            ParentId = g.ParentId,
            IsGroup = g.IsGroup,
            SelfValue = 0m,
            AggValue = 0m
        });

        // Unassigned node for items with no matching group
        var unassignedId = Guid.Empty;
        ItemGroupTreeNode? unassignedNode = null;

        foreach (var item in items)
        {
            if (!cogsByItemId.TryGetValue(item.Id, out var cogsDebit) || cogsDebit == 0)
            {
                continue;
            }

            Guid? matchedGroupId = null;
            if (item.ItemGroupId.HasValue && nodes.ContainsKey(item.ItemGroupId.Value))
            {
                matchedGroupId = item.ItemGroupId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(item.ItemGroup) && groupByName.TryGetValue(item.ItemGroup, out var matchedGroup))
            {
                matchedGroupId = matchedGroup.Id;
            }

            if (matchedGroupId.HasValue)
            {
                nodes[matchedGroupId.Value].SelfValue += cogsDebit;
            }
            else
            {
                if (unassignedNode == null)
                {
                    unassignedNode = new ItemGroupTreeNode
                    {
                        Id = unassignedId,
                        Name = "Unassigned",
                        ParentId = null,
                        IsGroup = false,
                        SelfValue = 0m,
                        AggValue = 0m
                    };
                }
                unassignedNode.SelfValue += cogsDebit;
            }
        }

        // Build parent-child relationships
        var childrenLookup = new Dictionary<Guid, List<ItemGroupTreeNode>>();
        var rootNodes = new List<ItemGroupTreeNode>();

        foreach (var node in nodes.Values)
        {
            if (node.ParentId.HasValue && nodes.ContainsKey(node.ParentId.Value))
            {
                if (!childrenLookup.TryGetValue(node.ParentId.Value, out var childList))
                {
                    childList = new List<ItemGroupTreeNode>();
                    childrenLookup[node.ParentId.Value] = childList;
                }
                childList.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        if (unassignedNode != null && unassignedNode.SelfValue > 0)
        {
            unassignedNode.AggValue = unassignedNode.SelfValue;
            rootNodes.Add(unassignedNode);
        }

        // Post-order calculation of agg_value
        decimal ComputeAggValue(ItemGroupTreeNode current)
        {
            var agg = current.SelfValue;
            if (childrenLookup.TryGetValue(current.Id, out var children))
            {
                foreach (var child in children)
                {
                    agg += ComputeAggValue(child);
                }
            }
            current.AggValue = agg;
            return agg;
        }

        foreach (var root in rootNodes)
        {
            ComputeAggValue(root);
        }

        var totalCogs = rootNodes.Sum(r => r.AggValue);

        // Flatten tree into rows
        var rows = new List<CogsByItemGroupRowDto>();

        void AppendRows(ItemGroupTreeNode current, int level)
        {
            if (current.AggValue <= 0)
            {
                return;
            }

            var pct = totalCogs > 0 ? Math.Round(current.AggValue / totalCogs * 100, 2) : 0;

            rows.Add(new CogsByItemGroupRowDto
            {
                ItemGroupId = current.Id == Guid.Empty ? null : current.Id,
                ItemGroupName = current.Name,
                CogsDebit = Math.Round(current.AggValue, 2),
                Indent = level,
                IsGroup = current.IsGroup,
                PercentageOfTotal = pct
            });

            // If group node has its own direct items and also child groups, show direct items line
            if (current.IsGroup && current.SelfValue > 0 && current.SelfValue < current.AggValue)
            {
                var directPct = totalCogs > 0 ? Math.Round(current.SelfValue / totalCogs * 100, 2) : 0;
                rows.Add(new CogsByItemGroupRowDto
                {
                    ItemGroupId = current.Id == Guid.Empty ? null : current.Id,
                    ItemGroupName = $"{current.Name} (Direct)",
                    CogsDebit = Math.Round(current.SelfValue, 2),
                    Indent = level + 1,
                    IsGroup = false,
                    PercentageOfTotal = directPct
                });
            }

            if (childrenLookup.TryGetValue(current.Id, out var children))
            {
                foreach (var child in children.OrderByDescending(c => c.AggValue))
                {
                    AppendRows(child, level + 1);
                }
            }
        }

        foreach (var root in rootNodes.OrderByDescending(r => r.AggValue))
        {
            AppendRows(root, 0);
        }

        var topGroup = rows.Where(r => r.Indent == 0 || !r.IsGroup).OrderByDescending(r => r.CogsDebit).FirstOrDefault();

        return new CogsByItemGroupReportDto
        {
            Rows = rows,
            TotalCogsDebit = Math.Round(totalCogs, 2),
            TotalItemGroups = rows.Count,
            TopItemGroupName = topGroup?.ItemGroupName,
            TopItemGroupDebit = topGroup?.CogsDebit ?? 0m
        };
    }

    private class ItemGroupTreeNode
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public bool IsGroup { get; set; }
        public decimal SelfValue { get; set; }
        public decimal AggValue { get; set; }
    }
}
