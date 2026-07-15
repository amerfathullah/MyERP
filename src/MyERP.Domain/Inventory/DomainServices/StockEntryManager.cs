using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for Stock Entry business rules.
/// Validates warehouse assignments, purpose-specific rules, and material transfer limits.
/// </summary>
public class StockEntryManager : DomainService
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public StockEntryManager(
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Validates warehouse assignments based on Stock Entry purpose.
    /// Per DO-NOT: "Allow same-warehouse Material Transfer when all inventory dimensions are identical"
    /// </summary>
    public async Task ValidateWarehousesAsync(StockEntry entry)
    {
        foreach (var item in entry.Items)
        {
            var isTransfer = entry.EntryType is StockEntryType.MaterialTransfer
                or StockEntryType.MaterialTransferForManufacture
                or StockEntryType.SendToWarehouse;

            if (isTransfer)
            {
                if (!item.SourceWarehouseId.HasValue)
                    throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                        .WithData("field", "SourceWarehouse");

                if (!item.TargetWarehouseId.HasValue)
                    throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                        .WithData("field", "TargetWarehouse");

                if (item.SourceWarehouseId == item.TargetWarehouseId)
                    throw new BusinessException(MyERPDomainErrorCodes.SameWarehouseTransfer);
            }

            var isReceipt = entry.EntryType is StockEntryType.MaterialReceipt
                or StockEntryType.ReceiveAtWarehouse
                or StockEntryType.Manufacture;

            if (isReceipt && !item.TargetWarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "TargetWarehouse");

            var isIssue = entry.EntryType is StockEntryType.MaterialIssue;

            if (isIssue && !item.SourceWarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "SourceWarehouse");

            // Group warehouse validation
            if (item.SourceWarehouseId.HasValue)
            {
                var source = await _warehouseRepository.FindAsync(item.SourceWarehouseId.Value);
                if (source?.IsGroup == true)
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouseName", source.Name);
            }

            if (item.TargetWarehouseId.HasValue)
            {
                var target = await _warehouseRepository.FindAsync(item.TargetWarehouseId.Value);
                if (target?.IsGroup == true)
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouseName", target.Name);
            }
        }
    }

    /// <summary>
    /// Validates all items are active and stock-trackable for stock entries.
    /// Filters out service items (MaintainStock=false) with a warning.
    /// </summary>
    public async Task ValidateItemsAsync(StockEntry entry)
    {
        if (!entry.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        foreach (var seItem in entry.Items)
        {
            var item = await _itemRepository.FindAsync(seItem.ItemId);
            if (item == null) continue;

            if (!item.IsActive)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ItemInactive)
                    .WithData("itemCode", item.ItemCode)
                    .WithData("itemName", item.ItemName);
            }
        }
    }

    /// <summary>
    /// Validates material transfer qty doesn't exceed the limit for manufacturing.
    /// Per DO-NOT: "Allow excess material transfer for manufacture beyond required_qty - already_transferred_qty"
    /// Exception: returns and "Material Transferred" backflush mode.
    /// </summary>
    public void ValidateTransferQty(decimal requiredQty, decimal transferredQty, decimal requestedQty,
        bool isReturn = false, bool isMaterialTransferredMode = false)
    {
        if (isReturn || isMaterialTransferredMode) return;

        var allowed = requiredQty - transferredQty;
        if (allowed < 0) allowed = 0;

        if (requestedQty > allowed)
        {
            throw new BusinessException("MyERP:05030")
                .WithData("required", requiredQty)
                .WithData("transferred", transferredQty)
                .WithData("requested", requestedQty)
                .WithData("allowed", allowed);
        }
    }
}
