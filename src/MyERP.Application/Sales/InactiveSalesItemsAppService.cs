using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Inactive Sales Items report application service.
/// Implements ERPNext accounts/report/inactive_sales_items.
/// PR #63466 (commit 119201aecc): excludes item templates (HasVariants = false).
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class InactiveSalesItemsAppService : ApplicationService, IInactiveSalesItemsAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Territory, Guid> _territoryRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;

    public InactiveSalesItemsAppService(
        IRepository<Item, Guid> itemRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Territory, Guid> territoryRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<Customer, Guid> customerRepository)
    {
        _itemRepository = itemRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _territoryRepository = territoryRepository;
        _itemGroupRepository = itemGroupRepository;
        _customerRepository = customerRepository;
    }

    public async Task<InactiveSalesItemsReportDto> GetReportAsync(InactiveSalesItemsFilterDto input)
    {
        if (input.DaysSinceLastOrder <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "'Days Since Last Order' must be greater than zero.");
        }

        var docType = string.IsNullOrWhiteSpace(input.DocType) ? "Sales Order" : input.DocType.Trim();
        var isSalesOrder = docType.Equals("Sales Order", StringComparison.OrdinalIgnoreCase);
        var isSalesInvoice = docType.Equals("Sales Invoice", StringComparison.OrdinalIgnoreCase);

        if (!isSalesOrder && !isSalesInvoice)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Invalid value '{input.DocType}' for 'DocType'.");
        }

        var asOfDate = (input.AsOfDate ?? Clock.Now).Date;

        // 1. Fetch Items: stock items, active, NOT templates (HasVariants == false, per PR #63466 / commit 119201aecc)
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.IsActive && i.MaintainStock && !i.HasVariants);

        if (input.CompanyId.HasValue)
            itemQuery = itemQuery.Where(i => i.CompanyId == input.CompanyId.Value);

        if (input.ItemId.HasValue)
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);

        if (input.ItemGroupId.HasValue)
            itemQuery = itemQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);

        var items = itemQuery.ToList();
        if (items.Count == 0)
        {
            return new InactiveSalesItemsReportDto
            {
                DocType = isSalesOrder ? "Sales Order" : "Sales Invoice",
                DaysSinceLastOrderThreshold = input.DaysSinceLastOrder
            };
        }

        var itemIds = items.Select(i => i.Id).ToHashSet();
        var itemGroupIds = items.Where(i => i.ItemGroupId.HasValue).Select(i => i.ItemGroupId!.Value).Distinct().ToList();

        var itemGroupMap = new Dictionary<Guid, string>();
        if (itemGroupIds.Count > 0)
        {
            var groups = (await _itemGroupRepository.GetQueryableAsync())
                .Where(g => itemGroupIds.Contains(g.Id))
                .ToList();
            itemGroupMap = groups.ToDictionary(g => g.Id, g => g.Name);
        }

        // Territory lookup
        var territoryMap = (await _territoryRepository.GetQueryableAsync())
            .ToDictionary(t => t.Id, t => t.Name);

        // Customer lookup
        var customerMap = (await _customerRepository.GetQueryableAsync())
            .ToDictionary(c => c.Id, c => (c.Name, c.TerritoryId));

        // 2. Fetch sales items
        var salesDetails = new List<(Guid ItemId, Guid? TerritoryId, string? CustomerName, DateTime OrderDate, decimal Qty)>();

        if (isSalesOrder)
        {
            var soQuery = await _salesOrderRepository.WithDetailsAsync(x => x.Items);
            var orders = soQuery.Where(so => so.Status != DocumentStatus.Draft
                                          && so.Status != DocumentStatus.Cancelled
                                          && (!input.CompanyId.HasValue || so.CompanyId == input.CompanyId.Value)
                                          && so.OrderDate <= asOfDate)
                                .ToList();

            foreach (var so in orders)
            {
                var custInfo = customerMap.GetValueOrDefault(so.CustomerId);
                var terrId = custInfo.TerritoryId;

                foreach (var line in so.Items.Where(i => itemIds.Contains(i.ItemId)))
                {
                    salesDetails.Add((line.ItemId, terrId, custInfo.Name, so.OrderDate, line.Quantity));
                }
            }
        }
        else
        {
            var siQuery = await _salesInvoiceRepository.WithDetailsAsync(x => x.Items);
            var invoices = siQuery.Where(si => si.Status != DocumentStatus.Draft
                                            && si.Status != DocumentStatus.Cancelled
                                            && !si.IsReturn
                                            && (!input.CompanyId.HasValue || si.CompanyId == input.CompanyId.Value)
                                            && si.IssueDate <= asOfDate)
                                  .ToList();

            foreach (var si in invoices)
            {
                var custInfo = customerMap.GetValueOrDefault(si.CustomerId);
                var terrId = custInfo.TerritoryId;

                foreach (var line in si.Items.Where(i => itemIds.Contains(i.ItemId)))
                {
                    salesDetails.Add((line.ItemId, terrId, custInfo.Name, si.IssueDate, line.Quantity));
                }
            }
        }

        // Filter sales by territory if specified
        if (input.TerritoryId.HasValue)
        {
            salesDetails = salesDetails.Where(s => s.TerritoryId == input.TerritoryId.Value).ToList();
        }

        var reportItems = new List<InactiveSalesItemDto>();

        foreach (var item in items)
        {
            var sales = salesDetails
                .Where(s => s.ItemId == item.Id)
                .OrderByDescending(s => s.OrderDate)
                .ToList();

            var latest = sales.FirstOrDefault();

            if (latest.ItemId != Guid.Empty)
            {
                var daysSince = (int)(asOfDate - latest.OrderDate.Date).TotalDays;
                // Only include if inactive past the threshold
                if (daysSince > input.DaysSinceLastOrder)
                {
                    reportItems.Add(new InactiveSalesItemDto
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        ItemGroup = item.ItemGroupId.HasValue ? itemGroupMap.GetValueOrDefault(item.ItemGroupId.Value) : null,
                        Territory = latest.TerritoryId.HasValue ? territoryMap.GetValueOrDefault(latest.TerritoryId.Value) : null,
                        CustomerName = latest.CustomerName,
                        LastOrderDate = latest.OrderDate,
                        Quantity = latest.Qty,
                        DaysSinceLastOrder = daysSince
                    });
                }
            }
            else
            {
                // Never ordered: always inactive
                reportItems.Add(new InactiveSalesItemDto
                {
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    ItemGroup = item.ItemGroupId.HasValue ? itemGroupMap.GetValueOrDefault(item.ItemGroupId.Value) : null,
                    Territory = null,
                    CustomerName = null,
                    LastOrderDate = null,
                    Quantity = null,
                    DaysSinceLastOrder = null
                });
            }
        }

        reportItems = reportItems.OrderByDescending(r => r.DaysSinceLastOrder ?? int.MaxValue)
            .ThenBy(r => r.ItemCode)
            .ToList();

        return new InactiveSalesItemsReportDto
        {
            Items = reportItems,
            TotalItems = reportItems.Count,
            DocType = isSalesOrder ? "Sales Order" : "Sales Invoice",
            DaysSinceLastOrderThreshold = input.DaysSinceLastOrder
        };
    }
}
