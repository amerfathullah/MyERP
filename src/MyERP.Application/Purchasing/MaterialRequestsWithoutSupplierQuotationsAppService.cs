using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Report service for Material Requests for which Supplier Quotations are not created.
/// Filters submitted Purchase Material Requests that do not have linked Supplier Quotation items.
/// Ported from ERPNext stock/report/material_requests_for_which_supplier_quotations_are_not_created.
/// </summary>
[Authorize(MyERPPermissions.MaterialRequests.Default)]
public class MaterialRequestsWithoutSupplierQuotationsAppService : ApplicationService, IMaterialRequestsWithoutSupplierQuotationsAppService
{
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IRepository<SupplierQuotation, Guid> _supplierQuotationRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public MaterialRequestsWithoutSupplierQuotationsAppService(
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<SupplierQuotation, Guid> supplierQuotationRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _materialRequestRepository = materialRequestRepository;
        _supplierQuotationRepository = supplierQuotationRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _companyRepository = companyRepository;
    }

    public async Task<MaterialRequestsWithoutSupplierQuotationsReportDto> GetReportAsync(MaterialRequestsWithoutSupplierQuotationsFilterDto input)
    {
        // 1. Collect all MaterialRequestItemIds quoted in active Supplier Quotations
        var sqQueryable = await _supplierQuotationRepository.WithDetailsAsync(sq => sq.Items);
        var quotedMrItemIds = sqQueryable
            .Where(sq => sq.Status != DocumentStatus.Cancelled)
            .SelectMany(sq => sq.Items)
            .Where(i => i.MaterialRequestItemId.HasValue)
            .Select(i => i.MaterialRequestItemId!.Value)
            .ToHashSet();

        // 2. Query submitted Purchase Material Requests
        var mrQueryable = await _materialRequestRepository.WithDetailsAsync(mr => mr.Items);
        var query = mrQueryable.Where(mr =>
            mr.RequestType == MaterialRequestType.Purchase &&
            mr.Status == DocumentStatus.Submitted);

        if (input.CompanyId.HasValue)
        {
            query = query.Where(mr => mr.CompanyId == input.CompanyId.Value);
        }

        if (input.FromDate.HasValue)
        {
            var from = input.FromDate.Value.Date;
            query = query.Where(mr => mr.RequestDate >= from);
        }

        if (input.ToDate.HasValue)
        {
            var to = input.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(mr => mr.RequestDate <= to);
        }

        var materialRequests = query.ToList();
        if (materialRequests.Count == 0)
        {
            return new MaterialRequestsWithoutSupplierQuotationsReportDto
            {
                CompanyId = input.CompanyId
            };
        }

        // 3. Resolve Company, Item, and Warehouse masters
        var companyIds = materialRequests.Select(mr => mr.CompanyId).Distinct().ToList();
        var companyQueryable = await _companyRepository.GetQueryableAsync();
        var companies = companyQueryable.Where(c => companyIds.Contains(c.Id)).ToList().ToDictionary(c => c.Id, c => c.Name);

        var itemIds = materialRequests.SelectMany(mr => mr.Items.Select(i => i.ItemId)).Distinct().ToList();
        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var items = itemQueryable.Where(i => itemIds.Contains(i.Id)).ToList().ToDictionary(i => i.Id);

        var warehouseIds = materialRequests
            .SelectMany(mr => mr.Items.Where(i => i.WarehouseId.HasValue).Select(i => i.WarehouseId!.Value))
            .Distinct()
            .ToList();
        var whQueryable = await _warehouseRepository.GetQueryableAsync();
        var warehouses = whQueryable.Where(w => warehouseIds.Contains(w.Id)).ToList().ToDictionary(w => w.Id, w => w.Name);

        var reportItems = new List<MaterialRequestsWithoutSupplierQuotationsItemDto>();

        foreach (var mr in materialRequests)
        {
            var companyName = companies.TryGetValue(mr.CompanyId, out var cName) ? cName : string.Empty;

            foreach (var mrItem in mr.Items)
            {
                // Exclude items already quoted in a Supplier Quotation
                if (quotedMrItemIds.Contains(mrItem.Id))
                    continue;

                if (input.WarehouseId.HasValue && mrItem.WarehouseId != input.WarehouseId.Value)
                    continue;

                items.TryGetValue(mrItem.ItemId, out var itemMaster);
                var warehouseName = mrItem.WarehouseId.HasValue && warehouses.TryGetValue(mrItem.WarehouseId.Value, out var whName)
                    ? whName
                    : null;

                reportItems.Add(new MaterialRequestsWithoutSupplierQuotationsItemDto
                {
                    MaterialRequestId = mr.Id,
                    RequestNumber = mr.RequestNumber,
                    RequestDate = mr.RequestDate,
                    RequiredByDate = mr.RequiredByDate,
                    ItemId = mrItem.ItemId,
                    ItemCode = itemMaster?.ItemCode ?? string.Empty,
                    ItemName = mrItem.ItemName,
                    Description = itemMaster?.Description,
                    Quantity = mrItem.Quantity,
                    Uom = mrItem.Uom,
                    WarehouseId = mrItem.WarehouseId,
                    WarehouseName = warehouseName,
                    CompanyId = mr.CompanyId,
                    CompanyName = companyName
                });
            }
        }

        // Apply optional search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            reportItems = reportItems.Where(r =>
                r.RequestNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.WarehouseName != null && r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Order by transaction_date asc matching ERPNext
        reportItems = reportItems.OrderBy(r => r.RequestDate).ThenBy(r => r.RequestNumber).ToList();

        var distinctRequestCount = reportItems.Select(r => r.MaterialRequestId).Distinct().Count();

        return new MaterialRequestsWithoutSupplierQuotationsReportDto
        {
            CompanyId = input.CompanyId,
            Items = reportItems,
            TotalPendingRequests = distinctRequestCount,
            TotalPendingItems = reportItems.Count,
            TotalPendingQuantity = reportItems.Sum(r => r.Quantity)
        };
    }
}
