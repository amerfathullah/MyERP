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

[Authorize(MyERPPermissions.Warehouses.Default)]
public class WarehouseWiseStockBalanceAppService : ApplicationService, IWarehouseWiseStockBalanceAppService
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public WarehouseWiseStockBalanceAppService(
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _warehouseRepository = warehouseRepository;
        _sleRepository = sleRepository;
        _companyRepository = companyRepository;
    }

    public async Task<WarehouseWiseStockBalanceReportDto> GetReportAsync(WarehouseWiseStockBalanceFilterDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new UserFriendlyException("Company is required.");
        }

        var company = await _companyRepository.FindAsync(input.CompanyId);
        if (company == null)
        {
            throw new UserFriendlyException("Company not found.");
        }

        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        warehouseQuery = warehouseQuery.Where(w => w.CompanyId == input.CompanyId);

        if (!input.ShowDisabledWarehouses)
        {
            warehouseQuery = warehouseQuery.Where(w => w.IsActive);
        }

        var warehouses = warehouseQuery.ToList();
        if (warehouses.Count == 0)
        {
            return new WarehouseWiseStockBalanceReportDto
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                AsOfDate = input.AsOfDate
            };
        }

        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId && !s.IsCancelled);

        if (input.AsOfDate.HasValue)
        {
            var asOfDate = input.AsOfDate.Value.Date;
            sleQuery = sleQuery.Where(s => s.PostingDate <= asOfDate);
        }

        var sleData = sleQuery
            .Select(s => new
            {
                s.WarehouseId,
                s.ItemId,
                s.StockValueDifference,
                s.QuantityChange
            })
            .ToList();

        var balanceByWarehouse = sleData
            .GroupBy(s => s.WarehouseId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    StockBalance = g.Sum(x => x.StockValueDifference),
                    Quantity = g.Sum(x => x.QuantityChange),
                    ItemCount = g.Select(x => x.ItemId).Distinct().Count()
                });

        var warehouseDict = warehouses.ToDictionary(w => w.Id);

        // Build nodes
        var nodeMap = warehouses.ToDictionary(w => w.Id, w =>
        {
            balanceByWarehouse.TryGetValue(w.Id, out var directStats);
            return new WarehouseTreeNode
            {
                Id = w.Id,
                Name = w.Name,
                Code = w.WarehouseCode,
                ParentId = w.ParentWarehouseId,
                IsGroup = w.IsGroup,
                IsActive = w.IsActive,
                DirectStockBalance = directStats?.StockBalance ?? 0m,
                DirectQuantity = directStats?.Quantity ?? 0m,
                ItemCount = directStats?.ItemCount ?? 0,
                StockBalance = directStats?.StockBalance ?? 0m,
                TotalQuantity = directStats?.Quantity ?? 0m
            };
        });

        // Link parent-child
        foreach (var node in nodeMap.Values)
        {
            if (node.ParentId.HasValue && nodeMap.TryGetValue(node.ParentId.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
                node.Parent = parentNode;
            }
        }

        // Filter subtree if specific warehouse specified
        List<WarehouseTreeNode> rootNodes;
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            if (nodeMap.TryGetValue(input.WarehouseId.Value, out var selectedNode))
            {
                rootNodes = new List<WarehouseTreeNode> { selectedNode };
            }
            else
            {
                rootNodes = new List<WarehouseTreeNode>();
            }
        }
        else
        {
            rootNodes = nodeMap.Values
                .Where(n => n.Parent == null)
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Post-order rollup: calculate StockBalance and TotalQuantity including descendants
        void Rollup(WarehouseTreeNode node)
        {
            foreach (var child in node.Children)
            {
                Rollup(child);
            }

            if (node.Children.Count > 0)
            {
                node.StockBalance = node.DirectStockBalance + node.Children.Sum(c => c.StockBalance);
                node.TotalQuantity = node.DirectQuantity + node.Children.Sum(c => c.TotalQuantity);
            }
        }

        foreach (var root in rootNodes)
        {
            Rollup(root);
        }

        // Flatten hierarchy in pre-order traversal
        var flattenedItems = new List<WarehouseStockBalanceItemDto>();

        void Flatten(WarehouseTreeNode node, int indent)
        {
            node.Indent = indent;
            flattenedItems.Add(new WarehouseStockBalanceItemDto
            {
                WarehouseId = node.Id,
                WarehouseName = node.Name,
                WarehouseCode = node.Code,
                ParentWarehouseId = node.ParentId,
                ParentWarehouseName = node.Parent?.Name,
                IsGroup = node.IsGroup,
                IsActive = node.IsActive,
                Indent = node.Indent,
                StockBalance = Math.Round(node.StockBalance, 2),
                DirectStockBalance = Math.Round(node.DirectStockBalance, 2),
                TotalQuantity = Math.Round(node.TotalQuantity, 4),
                DirectQuantity = Math.Round(node.DirectQuantity, 4),
                ItemCount = node.ItemCount
            });

            foreach (var child in node.Children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                Flatten(child, indent + 1);
            }
        }

        foreach (var root in rootNodes)
        {
            Flatten(root, 0);
        }

        var report = new WarehouseWiseStockBalanceReportDto
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            AsOfDate = input.AsOfDate,
            TotalStockBalance = Math.Round(rootNodes.Sum(r => r.StockBalance), 2),
            TotalQuantity = Math.Round(rootNodes.Sum(r => r.TotalQuantity), 4),
            TotalWarehouseCount = flattenedItems.Count,
            ActiveWarehouseCount = flattenedItems.Count(i => i.IsActive),
            Items = flattenedItems
        };

        return report;
    }

    private class WarehouseTreeNode
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public Guid? ParentId { get; set; }
        public bool IsGroup { get; set; }
        public bool IsActive { get; set; }
        public decimal DirectStockBalance { get; set; }
        public decimal DirectQuantity { get; set; }
        public int ItemCount { get; set; }
        public decimal StockBalance { get; set; }
        public decimal TotalQuantity { get; set; }
        public int Indent { get; set; }
        public WarehouseTreeNode? Parent { get; set; }
        public List<WarehouseTreeNode> Children { get; } = new();
    }
}
