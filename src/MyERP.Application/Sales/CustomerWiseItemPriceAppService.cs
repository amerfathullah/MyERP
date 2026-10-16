using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.Customers.Default)]
public class CustomerWiseItemPriceAppService : ApplicationService, ICustomerWiseItemPriceAppService
{
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<PriceList, Guid> _priceListRepo;
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;

    public CustomerWiseItemPriceAppService(
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<PriceList, Guid> priceListRepo,
        IRepository<ItemPrice, Guid> itemPriceRepo,
        IRepository<Bin, Guid> binRepo,
        IRepository<Warehouse, Guid> warehouseRepo)
    {
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
        _priceListRepo = priceListRepo;
        _itemPriceRepo = itemPriceRepo;
        _binRepo = binRepo;
        _warehouseRepo = warehouseRepo;
    }

    public async Task<CustomerWiseItemPriceReportDto> GetReportAsync(GetCustomerWiseItemPriceInput input)
    {
        var customer = await _customerRepo.FindAsync(input.CustomerId);
        if (customer == null)
        {
            throw new EntityNotFoundException(typeof(Customer), input.CustomerId);
        }

        // Resolve candidate price lists in priority order: Customer default -> Company default selling
        PriceList? customerPriceList = null;
        if (customer.DefaultPriceListId.HasValue)
        {
            customerPriceList = await _priceListRepo.FindAsync(customer.DefaultPriceListId.Value);
        }

        var plQuery = await _priceListRepo.GetQueryableAsync();
        var defaultSellingPriceList = plQuery.FirstOrDefault(pl =>
            pl.IsSelling
            && pl.IsActive
            && pl.IsDefault
            && (pl.CompanyId == input.CompanyId || pl.CompanyId == null));

        var candidatePriceLists = new List<PriceList>();
        if (customerPriceList != null && customerPriceList.IsActive)
        {
            candidatePriceLists.Add(customerPriceList);
        }
        if (defaultSellingPriceList != null && !candidatePriceLists.Any(pl => pl.Id == defaultSellingPriceList.Id))
        {
            candidatePriceLists.Add(defaultSellingPriceList);
        }

        // Query active selling items
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var items = itemQuery
            .Where(i => i.CompanyId == input.CompanyId && i.IsActive)
            .WhereIf(input.ItemId.HasValue, i => i.Id == input.ItemId)
            .WhereIf(input.ItemGroupId.HasValue, i => i.ItemGroupId == input.ItemGroupId)
            .OrderBy(i => i.ItemName)
            .ToList();

        var itemIds = items.Select(i => i.Id).ToList();

        // Query stock in company warehouses
        var whQuery = await _warehouseRepo.GetQueryableAsync();
        var warehouseIds = whQuery
            .Where(w => w.CompanyId == input.CompanyId && w.IsActive)
            .Select(w => w.Id)
            .ToList();

        var binQuery = await _binRepo.GetQueryableAsync();
        var stockMap = binQuery
            .Where(b => itemIds.Contains(b.ItemId) && warehouseIds.Contains(b.WarehouseId))
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, Available = g.Sum(b => b.ActualQty) })
            .ToDictionary(g => g.ItemId, g => g.Available);

        // Query active prices
        var today = DateTime.UtcNow.Date;
        var candidatePlIds = candidatePriceLists.Select(pl => pl.Id).ToList();
        var ipQuery = await _itemPriceRepo.GetQueryableAsync();
        var itemPrices = ipQuery
            .Where(ip => itemIds.Contains(ip.ItemId)
                && candidatePlIds.Contains(ip.PriceListId)
                && (ip.CustomerId == customer.Id || ip.CustomerId == null)
                && (ip.ValidFrom == null || ip.ValidFrom <= today)
                && (ip.ValidUpto == null || ip.ValidUpto >= today))
            .ToList();

        var rows = new List<CustomerWiseItemPriceRowDto>();

        foreach (var item in items)
        {
            decimal resolvedRate = 0m;
            string? resolvedPriceListName = null;

            foreach (var pl in candidatePriceLists)
            {
                // Customer-specific price first
                var custPrice = itemPrices.FirstOrDefault(ip =>
                    ip.ItemId == item.Id
                    && ip.PriceListId == pl.Id
                    && ip.CustomerId == customer.Id
                    && ip.PriceListRate > 0);

                if (custPrice != null)
                {
                    resolvedRate = custPrice.PriceListRate;
                    resolvedPriceListName = pl.Name;
                    break;
                }

                // General price in this list
                var genPrice = itemPrices.FirstOrDefault(ip =>
                    ip.ItemId == item.Id
                    && ip.PriceListId == pl.Id
                    && ip.CustomerId == null
                    && ip.PriceListRate > 0);

                if (genPrice != null)
                {
                    resolvedRate = genPrice.PriceListRate;
                    resolvedPriceListName = pl.Name;
                    break;
                }
            }

            // Fallback to standard selling price if no price list rate
            if (resolvedRate == 0m && item.StandardSellingPrice.HasValue && item.StandardSellingPrice.Value > 0m)
            {
                resolvedRate = item.StandardSellingPrice.Value;
                resolvedPriceListName = customerPriceList?.Name ?? defaultSellingPriceList?.Name;
            }

            var stock = stockMap.GetValueOrDefault(item.Id, 0m);

            rows.Add(new CustomerWiseItemPriceRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                SellingRate = resolvedRate,
                PriceListName = resolvedPriceListName,
                AvailableStock = stock,
                StockUom = item.Uom
            });
        }

        return new CustomerWiseItemPriceReportDto
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            DefaultPriceListName = customerPriceList?.Name ?? defaultSellingPriceList?.Name,
            Rows = rows,
            TotalItems = rows.Count,
            TotalAvailableStock = rows.Sum(r => r.AvailableStock)
        };
    }
}
