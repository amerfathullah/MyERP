using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class PickListAppService : ApplicationService, IPickListAppService
{
    private readonly IRepository<PickList, Guid> _repository;

    public PickListAppService(IRepository<PickList, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PickListDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(p => p.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(p => p.PickListNumber != null && p.PickListNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(p => p.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PickListDto>(totalCount, items.Select(x => ObjectMapper.Map<PickList, PickListDto>(x)).ToList());
    }

    public async Task<PickListDto> GetAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<PickListDto> CreateAsync(CreatePickListDto input)
    {
        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        var itemIds = input.Items.Select(i => i.ItemId).ToArray();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        // Company-restriction check: every other transaction AppService referencing a
        // company-restricted master wires this in. Per ERPNext pick_list.py validate_warehouses:
        // each row's Warehouse must belong to the Pick List's own company.
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "PickList", input.CompanyId,
            itemIds: itemIds,
            customerIds: input.CustomerId.HasValue ? new[] { input.CustomerId.Value } : null,
            warehouseIds: input.Items.Select(i => i.WarehouseId).Distinct().ToArray());

        // SRE conflict check per gotcha #3533
        if (input.SalesOrderId.HasValue)
        {
            var sreRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockReservationEntry, Guid>>();
            var activeSre = await sreRepo.FindAsync(s =>
                s.VoucherType == "SalesOrder"
                && s.VoucherId == input.SalesOrderId.Value
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0);

            if (activeSre != null)
            {
                throw new Volo.Abp.BusinessException("MyERP:13020")
                    .WithData("reason", "Cannot create Pick List for Sales Order because stock has already been reserved via Stock Reservation Entries. Deliver directly against the reservation or cancel reservations first.");
            }
        }

        var pl = new PickList(GuidGenerator.Create(), input.CompanyId, input.Purpose, CurrentTenant.Id)
        {
            SalesOrderId = input.SalesOrderId,
            MaterialRequestId = input.MaterialRequestId,
            WorkOrderId = input.WorkOrderId,
            CustomerId = input.CustomerId,
            PickManually = input.PickManually,
        };
        foreach (var item in input.Items)
            pl.AddItem(item.ItemId, item.WarehouseId, item.Qty, itemName: item.ItemName, batchId: item.BatchId,
                productBundleItemId: item.ProductBundleItemId, sourceDocumentItemId: item.SourceDocumentItemId,
                serialAndBatchBundleId: item.SerialAndBatchBundleId);
        await _repository.InsertAsync(pl);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<PickListDto> SubmitAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pl.Submit();
        await _repository.UpdateAsync(pl);
        if (pl.SalesOrderId.HasValue)
        {
            await UpdateSalesOrderPickingStatusAsync(pl, isSubmit: true);
        }
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<PickListDto> CancelAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        pl.Cancel();
        await _repository.UpdateAsync(pl);
        if (pl.SalesOrderId.HasValue)
        {
            await UpdateSalesOrderPickingStatusAsync(pl, isSubmit: false);
        }
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    private async Task<Dictionary<Guid, decimal>> BuildBundleComponentStockQtyMapAsync(
        MyERP.Sales.Entities.ProductBundle bundle,
        IRepository<Inventory.Entities.Item, Guid> itemRepo,
        Inventory.DomainServices.UomConversionService? uomService)
    {
        var componentStockQtyMap = new Dictionary<Guid, decimal>();
        var compIds = bundle.Items.Select(x => x.ComponentItemId).Distinct().ToList();
        var compItems = (await itemRepo.GetQueryableAsync())
            .Where(i => compIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        foreach (var comp in bundle.Items)
        {
            var factor = 1m;
            if (compItems.TryGetValue(comp.ComponentItemId, out var compItem)
                && !string.IsNullOrWhiteSpace(comp.Uom)
                && uomService != null)
            {
                factor = await uomService.GetConversionFactorAsync(compItem.Id, comp.Uom, compItem.Uom, compItem.VariantOfId);
            }
            componentStockQtyMap[comp.ComponentItemId] = comp.Qty * factor;
        }

        return componentStockQtyMap;
    }

    /// <summary>
    /// Updates picked quantities on linked Sales Order items.
    /// Per ERPNext PR #62149 / commit 05bc9ce0fe:
    /// - Counts picked bundles against the version recorded on the order row, not the item's current active version.
    /// - Converts component UOM to stock UOM before computing bundle completion.
    /// </summary>
    private async Task UpdateSalesOrderPickingStatusAsync(PickList pl, bool isSubmit)
    {
        if (!pl.SalesOrderId.HasValue) return;

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        var so = await soRepo.GetAsync(pl.SalesOrderId.Value);

        var bundleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.ProductBundle, Guid>>();
        var bundleQueryable = await bundleRepo.GetQueryableAsync();

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
        var uomService = LazyServiceProvider.LazyGetService<Inventory.DomainServices.UomConversionService>();

        var direction = isSubmit ? 1m : -1m;

        // Group pick list items by source document item id
        var pickedBySoItem = pl.Items
            .Where(i => i.SourceDocumentItemId.HasValue)
            .GroupBy(i => i.SourceDocumentItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var soItem in so.Items)
        {
            if (!pickedBySoItem.TryGetValue(soItem.Id, out var pickRows))
            {
                continue;
            }

            // Check if this SO row is a Product Bundle
            // Count picked bundles against the version recorded on the order row,
            // not the item's current active version (PR #62149)
            MyERP.Sales.Entities.ProductBundle? bundle = null;
            if (soItem.ProductBundleId.HasValue)
            {
                bundle = bundleQueryable.FirstOrDefault(b => b.Id == soItem.ProductBundleId.Value);
            }
            bundle ??= bundleQueryable.FirstOrDefault(b => b.ItemId == soItem.ItemId && b.IsActive && !b.IsDisabled);

            if (bundle != null && bundle.Items.Count > 0)
            {
                var componentStockQtyMap = await BuildBundleComponentStockQtyMapAsync(bundle, itemRepo, uomService);
                var bundlePickedCount = PickListManager.ComputePickedQtyForBundle(
                    pickRows, soItem.Id, componentStockQtyMap);

                soItem.UpdatePickedQty(direction * bundlePickedCount);
            }
            else
            {
                var standardPickedQty = pickRows.Sum(r => r.Qty);
                soItem.UpdatePickedQty(direction * standardPickedQty);
            }
        }

        await soRepo.UpdateAsync(so);
    }

    /// <summary>
    /// Allocates stock to pick list items, checking warehouse availability
    /// and deducting quantities already picked by other active pick lists.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Default)]
    public async Task<PickAllocationResultDto> AllocateStockAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        var pickListManager = LazyServiceProvider.LazyGetRequiredService<PickListManager>();
        var result = await pickListManager.AllocateStockAsync(pl);

        return new PickAllocationResultDto
        {
            HasShortage = result.HasShortage,
            Allocations = result.Allocations.Select(a => new PickAllocationDto
            {
                ItemId = a.ItemId,
                WarehouseId = a.WarehouseId,
                RequestedQty = a.RequestedQty,
                AllocatedQty = a.AllocatedQty,
                ShortageQty = a.ShortageQty,
            }).ToList(),
        };
    }

    /// <summary>
    /// Gets detailed stock availability insights including breakdown of stock held by other pick lists.
    /// Maps to ERPNext get_stock_availability / get_pick_list_holders logic.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Default)]
    [Volo.Abp.Uow.UnitOfWork]
    public async Task<List<StockAvailabilityInsightDto>> GetStockAvailabilityInsightAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        var pickListManager = LazyServiceProvider.LazyGetRequiredService<PickListManager>();
        var sreRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockReservationEntry, Guid>>();
        var insights = await pickListManager.GetStockAvailabilityInsightAsync(pl, sreRepo);

        return insights.Select(i => new StockAvailabilityInsightDto
        {
            ItemId = i.ItemId,
            ItemName = i.ItemName,
            WarehouseId = i.WarehouseId,
            WarehouseName = i.WarehouseName,
            ActualQty = i.ActualQty,
            PickedQty = i.PickedQty,
            ReservedQty = i.ReservedQty,
            FreeQty = i.FreeQty,
            HoldingPickLists = i.HoldingPickLists.Select(h => new HoldingPickListDto
            {
                PickListId = h.PickListId,
                PickListNumber = h.PickListNumber,
                Status = h.Status,
                WarehouseId = h.WarehouseId,
                HoldingQty = h.HoldingQty
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Gets pending transfer quantities for creating Stock Entries from this Pick List.
    /// Per DO-NOT: partial transfers are supported — map only pending qty per row.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Default)]
    public async Task<List<PendingTransferDto>> GetPendingTransfersAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);
        var pickListManager = LazyServiceProvider.LazyGetRequiredService<PickListManager>();
        var pending = pickListManager.GetPendingTransfers(pl);

        return pending.Select(pt => new PendingTransferDto
        {
            PickListItemId = pt.PickListItemId,
            ItemId = pt.ItemId,
            WarehouseId = pt.WarehouseId,
            PendingQty = pt.PendingQty,
            BatchId = pt.BatchId,
        }).ToList();
    }

    /// <summary>
    /// Creates a Delivery Note from a submitted Pick List.
    /// Per ERPNext PR #57412: maps customer from Pick List when no Sales Order is linked.
    /// Per ERPNext PR #60311 / commit 4eb8ba500c: enforce pick list read permissions when generating delivery notes.
    /// Per ERPNext PR #62326 / commit e3c6082498: maps only undelivered picked quantities (Qty - DeliveredQty)
    /// for both plain items and Product Bundles (sizing parent bundle rows via ComputePickedQtyForBundle(..., pendingOnly: true)).
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    [Volo.Abp.Uow.UnitOfWork]
    public async Task<Guid> CreateDeliveryNoteFromPickListAsync(Guid pickListId, Guid? salesOrderId = null)
    {
        await AuthorizationService.CheckAsync(MyERPPermissions.StockEntries.Default);

        var pl = await _repository.GetAsync(pickListId, includeDetails: true);

        if (pl.Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (pl.Purpose != "Delivery")
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Delivery purpose Pick Lists can create Delivery Notes");

        if (salesOrderId.HasValue && pl.SalesOrderId.HasValue && pl.SalesOrderId.Value != salesOrderId.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Specified Sales Order does not match the Pick List Sales Order.");
        }

        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>>();

        var targetSalesOrderId = salesOrderId ?? pl.SalesOrderId;

        // Resolve customer: SO takes priority, then Pick List customer (per PR #57412)
        Guid? customerId = null;
        Guid? warehouseId = null;
        MyERP.Sales.Entities.SalesOrder? targetSo = null;

        if (targetSalesOrderId.HasValue)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
            targetSo = await soRepo.GetAsync(targetSalesOrderId.Value);
            customerId = targetSo.CustomerId;
            warehouseId = targetSo.Items.FirstOrDefault(i => i.WarehouseId.HasValue)?.WarehouseId;
        }

        // Per PR #57412: when no SO, use Pick List's own customer
        if (!customerId.HasValue)
            customerId = pl.CustomerId;

        if (!customerId.HasValue)
            throw new BusinessException("MyERP:01007")
                .WithData("documentType", "Delivery Note — no customer on Pick List or linked Sales Order");

        // Resolve warehouse from first pick list item
        warehouseId ??= pl.Items.FirstOrDefault()?.WarehouseId;
        if (!warehouseId.HasValue)
            throw new BusinessException("MyERP:01007")
                .WithData("documentType", "Delivery Note — no warehouse on Pick List items");

        var dnNumber = await numberGenerator.GenerateAsync("DeliveryNote", pl.CompanyId);

        var dn = new MyERP.Sales.Entities.DeliveryNote(
            GuidGenerator.Create(),
            pl.CompanyId,
            customerId.Value,
            warehouseId.Value,
            dnNumber,
            Clock.Now.Date,
            pl.TenantId);

        dn.SalesOrderId = targetSalesOrderId;
        dn.PickListId = pl.Id;

        if (targetSo != null)
        {
            var bundleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.ProductBundle, Guid>>();
            var bundleQueryable = await bundleRepo.GetQueryableAsync();
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            var uomService = LazyServiceProvider.LazyGetService<Inventory.DomainServices.UomConversionService>();

            var pickedBySoItem = pl.Items
                .Where(i => i.SourceDocumentItemId.HasValue)
                .GroupBy(i => i.SourceDocumentItemId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var soItem in targetSo.Items)
            {
                if (!pickedBySoItem.TryGetValue(soItem.Id, out var pickRows))
                    continue;

                MyERP.Sales.Entities.ProductBundle? bundle = null;
                if (soItem.ProductBundleId.HasValue)
                {
                    bundle = bundleQueryable.FirstOrDefault(b => b.Id == soItem.ProductBundleId.Value);
                }
                bundle ??= bundleQueryable.FirstOrDefault(b => b.ItemId == soItem.ItemId && b.IsActive && !b.IsDisabled);

                if (bundle != null && bundle.Items.Count > 0)
                {
                    // Per ERPNext commit e3c6082498: count picked minus delivered per component row and skip bundles with nothing left
                    var componentStockQtyMap = await BuildBundleComponentStockQtyMapAsync(bundle, itemRepo, uomService);
                    var pendingBundles = PickListManager.ComputePickedQtyForBundle(
                        pickRows, soItem.Id, componentStockQtyMap, pendingOnly: true);

                    if (pendingBundles <= 0)
                        continue;

                    var perBundleTax = soItem.Quantity != 0
                        ? Math.Round(soItem.TaxAmount / soItem.Quantity * pendingBundles, 2)
                        : 0m;

                    dn.AddItem(
                        soItem.ItemId,
                        soItem.Description ?? string.Empty,
                        pendingBundles,
                        soItem.UnitPrice,
                        perBundleTax,
                        soItem.Uom ?? "Unit",
                        salesOrderItemId: soItem.Id,
                        pickListItemId: null);

                    var addedBundleRow = dn.Items[^1];
                    addedBundleRow.StockUom = soItem.StockUom;
                    addedBundleRow.ConversionFactor = soItem.ConversionFactor > 0 ? soItem.ConversionFactor : 1m;
                    addedBundleRow.WarehouseId = soItem.WarehouseId ?? pickRows.First().WarehouseId;
                }
                else
                {
                    var rowFactor = soItem.ConversionFactor > 0 ? soItem.ConversionFactor : 1m;
                    foreach (var pickRow in pickRows)
                    {
                        var pendingStockQty = Math.Max(0m, pickRow.Qty - Math.Max(pickRow.TransferredQty, pickRow.DeliveredQty));
                        if (pendingStockQty <= 0)
                            continue;

                        var pendingOrderUomQty = Math.Round(pendingStockQty / rowFactor, 6);
                        if (pendingOrderUomQty <= 0)
                            continue;

                        var rowTax = soItem.Quantity != 0
                            ? Math.Round(soItem.TaxAmount / soItem.Quantity * pendingOrderUomQty, 2)
                            : 0m;

                        dn.AddItem(
                            pickRow.ItemId,
                            soItem.Description ?? pickRow.ItemName ?? string.Empty,
                            pendingOrderUomQty,
                            soItem.UnitPrice,
                            rowTax,
                            soItem.Uom ?? "Unit",
                            salesOrderItemId: soItem.Id,
                            pickListItemId: pickRow.Id);

                        var addedRow = dn.Items[^1];
                        addedRow.StockUom = soItem.StockUom;
                        addedRow.ConversionFactor = rowFactor;
                        addedRow.WarehouseId = pickRow.WarehouseId;
                        addedRow.BatchId = pickRow.BatchId;
                    }
                }
            }
        }
        else
        {
            foreach (var pickRow in pl.Items)
            {
                var pendingQty = Math.Max(0m, pickRow.Qty - Math.Max(pickRow.TransferredQty, pickRow.DeliveredQty));
                if (pendingQty <= 0)
                    continue;

                dn.AddItem(
                    pickRow.ItemId,
                    pickRow.ItemName ?? string.Empty,
                    pendingQty,
                    0m,
                    0m,
                    "Unit",
                    salesOrderItemId: pickRow.SourceDocumentItemId,
                    pickListItemId: pickRow.Id);

                var addedRow = dn.Items[^1];
                addedRow.WarehouseId = pickRow.WarehouseId;
                addedRow.BatchId = pickRow.BatchId;
            }
        }

        if (dn.Items.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        await dnRepo.InsertAsync(dn, autoSave: true);
        return dn.Id;
    }

    /// <summary>
    /// Creates Stock Reservation Entries for Sales Order Items against this Pick List.
    /// Per ERPNext PR #59134: bundle components reserve against their Packed Item,
    /// because the bundle itself is a non-stock Sales Order Item and cannot hold reserved stock.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Submit)]
    [Volo.Abp.Uow.UnitOfWork]
    public async Task<PickListDto> CreateStockReservationEntriesAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);

        if (pl.Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion)
                .WithData("detail", "Pick List must be submitted to create stock reservations.");

        if (!pl.SalesOrderId.HasValue)
            throw new BusinessException("MyERP:01007")
                .WithData("documentType", "Pick List must be linked to a Sales Order to reserve stock");

        var sreManager = LazyServiceProvider.LazyGetRequiredService<StockReservationManager>();

        foreach (var item in pl.Items)
        {
            var qtyToReserve = item.Qty - item.StockReservedQty;
            if (qtyToReserve <= 0) continue;

            // Per PR #59134: product_bundle_item or sales_order_item
            var voucherDetailId = item.ProductBundleItemId ?? item.SourceDocumentItemId;

            await sreManager.ReserveStockFromPickListAsync(
                item.ItemId,
                item.WarehouseId,
                pl.CompanyId,
                qtyToReserve,
                voucherType: "SalesOrder",
                voucherId: pl.SalesOrderId.Value,
                fromVoucherType: "Pick List",
                fromVoucherId: pl.Id,
                batchId: item.BatchId,
                voucherDetailId: voucherDetailId,
                fromVoucherDetailId: item.Id,
                tenantId: pl.TenantId);

            item.SetStockReservedQty(item.StockReservedQty + qtyToReserve);
        }

        await _repository.UpdateAsync(pl);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }

    /// <summary>
    /// Cancels stock reservations originating from this Pick List (per ERPNext PR #59134).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    [Volo.Abp.Uow.UnitOfWork]
    public async Task<PickListDto> CancelStockReservationEntriesAsync(Guid id)
    {
        var pl = (await _repository.WithDetailsAsync()).First(p => p.Id == id);

        if (pl.Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Pick List must be submitted to cancel its stock reservations.");

        var sreManager = LazyServiceProvider.LazyGetRequiredService<StockReservationManager>();
        await sreManager.CancelReservationsFromVoucherAsync("Pick List", pl.Id);

        foreach (var item in pl.Items)
        {
            item.SetStockReservedQty(0m);
        }

        await _repository.UpdateAsync(pl);
        return ObjectMapper.Map<PickList, PickListDto>(pl);
    }
}
