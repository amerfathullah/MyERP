using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Serial No Warranty and Service Contract (AMC) Expiry report application service.
/// Tracks warranty status, AMC coverage, and expiring serial numbers.
/// Maps to ERPNext stock/report/serial_no_warranty_expiry and serial_no_service_contract_expiry.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class SerialNoWarrantyExpiryAppService : ApplicationService, ISerialNoWarrantyExpiryAppService
{
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;

    public SerialNoWarrantyExpiryAppService(
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Customer, Guid> customerRepository)
    {
        _serialNoRepository = serialNoRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _customerRepository = customerRepository;
    }

    public async Task<SerialNoWarrantyExpiryReportDto> GetReportAsync(SerialNoWarrantyExpiryFilterDto input)
    {
        var asOfDate = DateTime.UtcNow;

        var snQuery = await _serialNoRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            snQuery = snQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        }
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            snQuery = snQuery.Where(s => s.ItemId == input.ItemId.Value);
        }
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            snQuery = snQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);
        }
        if (input.CustomerId.HasValue && input.CustomerId.Value != Guid.Empty)
        {
            snQuery = snQuery.Where(s => s.CustomerId == input.CustomerId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLower();
            snQuery = snQuery.Where(s => s.SerialNumber.ToLower().Contains(term));
        }

        var serials = snQuery.ToList();
        if (serials.Count == 0)
        {
            return new SerialNoWarrantyExpiryReportDto { AsOfDate = asOfDate };
        }

        var itemIds = serials.Select(s => s.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id))).ToDictionary(i => i.Id);

        var whIds = serials.Where(s => s.WarehouseId.HasValue).Select(s => s.WarehouseId!.Value).Distinct().ToList();
        var whMap = whIds.Count > 0
            ? (await _warehouseRepository.GetListAsync(w => whIds.Contains(w.Id))).ToDictionary(w => w.Id)
            : new Dictionary<Guid, Warehouse>();

        var custIds = serials.Where(s => s.CustomerId.HasValue).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var custMap = custIds.Count > 0
            ? (await _customerRepository.GetListAsync(c => custIds.Contains(c.Id))).ToDictionary(c => c.Id)
            : new Dictionary<Guid, Customer>();

        var rows = new List<SerialNoWarrantyExpiryRowDto>();

        foreach (var s in serials)
        {
            s.UpdateMaintenanceStatus(asOfDate);

            int? daysToWarranty = s.WarrantyExpiryDate.HasValue
                ? (s.WarrantyExpiryDate.Value.Date - asOfDate.Date).Days
                : null;
            bool isWarrantyExpired = daysToWarranty.HasValue && daysToWarranty.Value < 0;

            int? daysToAmc = s.AmcExpiryDate.HasValue
                ? (s.AmcExpiryDate.Value.Date - asOfDate.Date).Days
                : null;
            bool isAmcExpired = daysToAmc.HasValue && daysToAmc.Value < 0;

            // Filter by ExpiryType
            if (string.Equals(input.ExpiryType, "Warranty", StringComparison.OrdinalIgnoreCase) && !s.WarrantyExpiryDate.HasValue)
                continue;
            if (string.Equals(input.ExpiryType, "AMC", StringComparison.OrdinalIgnoreCase) && !s.AmcExpiryDate.HasValue)
                continue;
            if (string.Equals(input.ExpiryType, "Both", StringComparison.OrdinalIgnoreCase) && !s.WarrantyExpiryDate.HasValue && !s.AmcExpiryDate.HasValue)
                continue;

            // Filter by MaintenanceStatus
            if (!string.IsNullOrWhiteSpace(input.MaintenanceStatus) && !string.Equals(input.MaintenanceStatus, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(s.MaintenanceStatus, input.MaintenanceStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Filter by DaysThreshold (e.g. expiring within N days)
            if (input.DaysThreshold.HasValue && input.DaysThreshold.Value > 0)
            {
                var threshold = input.DaysThreshold.Value;
                bool withinWarranty = daysToWarranty.HasValue && daysToWarranty.Value >= 0 && daysToWarranty.Value <= threshold;
                bool withinAmc = daysToAmc.HasValue && daysToAmc.Value >= 0 && daysToAmc.Value <= threshold;
                if (!withinWarranty && !withinAmc)
                    continue;
            }

            // Date range filter
            if (input.FromDate.HasValue || input.ToDate.HasValue)
            {
                var fromDate = input.FromDate?.Date;
                var toDate = input.ToDate?.Date;

                bool warrantyInRange = s.WarrantyExpiryDate.HasValue &&
                    (!fromDate.HasValue || s.WarrantyExpiryDate.Value.Date >= fromDate.Value) &&
                    (!toDate.HasValue || s.WarrantyExpiryDate.Value.Date <= toDate.Value);

                bool amcInRange = s.AmcExpiryDate.HasValue &&
                    (!fromDate.HasValue || s.AmcExpiryDate.Value.Date >= fromDate.Value) &&
                    (!toDate.HasValue || s.AmcExpiryDate.Value.Date <= toDate.Value);

                if (!warrantyInRange && !amcInRange)
                    continue;
            }

            itemMap.TryGetValue(s.ItemId, out var item);
            whMap.TryGetValue(s.WarehouseId ?? Guid.Empty, out var wh);
            custMap.TryGetValue(s.CustomerId ?? Guid.Empty, out var cust);

            rows.Add(new SerialNoWarrantyExpiryRowDto
            {
                SerialNoId = s.Id,
                SerialNumber = s.SerialNumber,
                ItemId = s.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                CustomerId = s.CustomerId,
                CustomerName = cust?.Name,
                WarehouseId = s.WarehouseId,
                WarehouseName = wh?.Name,
                WarrantyExpiryDate = s.WarrantyExpiryDate,
                DaysToWarrantyExpiry = daysToWarranty,
                IsWarrantyExpired = isWarrantyExpired,
                AmcExpiryDate = s.AmcExpiryDate,
                DaysToAmcExpiry = daysToAmc,
                IsAmcExpired = isAmcExpired,
                MaintenanceStatus = s.MaintenanceStatus,
                Status = s.Status,
                PurchaseDocumentType = s.PurchaseDocumentType,
                DeliveryDocumentType = s.DeliveryDocumentType
            });
        }

        var underWarrantyCount = rows.Count(r => r.WarrantyExpiryDate.HasValue && !r.IsWarrantyExpired);
        var outOfWarrantyCount = rows.Count(r => r.WarrantyExpiryDate.HasValue && r.IsWarrantyExpired);
        var underAmcCount = rows.Count(r => r.AmcExpiryDate.HasValue && !r.IsAmcExpired);
        var outOfAmcCount = rows.Count(r => r.AmcExpiryDate.HasValue && r.IsAmcExpired);
        var expiringSoonCount = rows.Count(r =>
            (r.DaysToWarrantyExpiry.HasValue && r.DaysToWarrantyExpiry.Value >= 0 && r.DaysToWarrantyExpiry.Value <= 30) ||
            (r.DaysToAmcExpiry.HasValue && r.DaysToAmcExpiry.Value >= 0 && r.DaysToAmcExpiry.Value <= 30));

        return new SerialNoWarrantyExpiryReportDto
        {
            AsOfDate = asOfDate,
            TotalCount = rows.Count,
            UnderWarrantyCount = underWarrantyCount,
            OutOfWarrantyCount = outOfWarrantyCount,
            UnderAmcCount = underAmcCount,
            OutOfAmcCount = outOfAmcCount,
            ExpiringSoonCount = expiringSoonCount,
            Rows = rows
        };
    }
}
