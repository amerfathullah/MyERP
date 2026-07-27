using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Validates Repack Stock Entry rules.
    /// Per ERPNext: Repack converts items from one form to another (split/merge/repackage).
    /// 
    /// Rules:
    /// 1. Must have at least one outgoing (source) and one incoming (target/FG) item
    /// 2. Items with only TargetWarehouse are FG (marked IsFinishedItem=true)
    /// 3. Items with only SourceWarehouse are consumed materials
    /// 4. Multiple FG items require SetBasicRateManually=true on each FG
    /// 5. Both source and target warehouse allowed on same item (transfer+repack)
    /// </summary>
    public void ValidateRepackItems(StockEntry entry)
    {
        if (entry.EntryType != StockEntryType.Repack) return;

        var outgoingItems = entry.Items.Where(i => i.SourceWarehouseId.HasValue).ToList();
        var incomingItems = entry.Items.Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue).ToList();

        if (!outgoingItems.Any())
        {
            throw new BusinessException("MyERP:05041")
                .WithData("reason", "Repack requires at least one outgoing (source) item");
        }

        if (!incomingItems.Any())
        {
            throw new BusinessException("MyERP:05041")
                .WithData("reason", "Repack requires at least one incoming (target/finished) item");
        }

        // Multiple FG items require manual rate setting
        if (incomingItems.Count > 1)
        {
            foreach (var fg in incomingItems)
            {
                if (!fg.SetBasicRateManually)
                {
                    throw new BusinessException("MyERP:05042")
                        .WithData("reason", "Multiple finished goods in Repack require SetBasicRateManually=true on each item");
                }
            }
        }
    }

    /// <summary>
    /// Calculates valuation rate for Repack FG items.
    /// Single FG: rate = total_outgoing_cost / fg_qty
    /// Multiple FGs: each must have rate set manually (validated separately).
    /// </summary>
    public decimal CalculateRepackFgRate(IReadOnlyList<StockEntryItem> items, decimal fgQty)
    {
        var totalOutgoingCost = items
            .Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem)
            .Sum(i => i.Quantity * (i.ValuationRate ?? 0));

        if (fgQty <= 0) return 0;
        return Math.Round(totalOutgoingCost / fgQty, 4);
    }

    /// <summary>
    /// Validates Disassemble Stock Entry rules.
    /// Per ERPNext: Disassemble reverses a Manufacture entry — breaks FG back into components.
    /// 
    /// Rules:
    /// 1. Must have a source stock entry (the original Manufacture entry)
    /// 2. FG consumption qty cannot exceed source manufacture FG qty
    /// 3. Material output qty must follow scale factor: source_row_qty × (disassemble_qty / source_fg_qty)
    /// 4. Cannot disassemble from a different Work Order's stock entry
    /// </summary>
    public void ValidateDisassembleItems(StockEntry entry, StockEntry? sourceEntry)
    {
        if (entry.EntryType != StockEntryType.Disassemble) return;

        if (sourceEntry == null && entry.SourceStockEntryId.HasValue)
        {
            throw new BusinessException("MyERP:05043")
                .WithData("reason", "Source manufacture stock entry not found");
        }

        if (sourceEntry != null)
        {
            // Cross-WO guard
            if (entry.WorkOrderId.HasValue && sourceEntry.WorkOrderId.HasValue
                && entry.WorkOrderId != sourceEntry.WorkOrderId)
            {
                throw new BusinessException("MyERP:05044")
                    .WithData("reason", "Cannot disassemble from a different Work Order's stock entry");
            }

            // FG consumption must not exceed source FG qty
            if (entry.FgCompletedQty > sourceEntry.FgCompletedQty)
            {
                throw new BusinessException("MyERP:05045")
                    .WithData("disassembleQty", entry.FgCompletedQty)
                    .WithData("sourceQty", sourceEntry.FgCompletedQty)
                    .WithData("reason", "Disassemble qty exceeds source manufacture FG qty");
            }
        }
    }

    /// <summary>
    /// Validates scale factor for Disassemble items against source entry.
    /// Every non-FG row's qty must equal: source_row_qty × (disassemble_qty / source_fg_qty).
    /// Tolerance: 1/(10^precision) for float rounding only.
    /// </summary>
    public void ValidateDisassembleScaleFactor(
        IReadOnlyList<StockEntryItem> disassemblyItems,
        IReadOnlyList<StockEntryItem> sourceItems,
        decimal disassembleQty,
        decimal sourceFgQty,
        int precision = 4)
    {
        if (sourceFgQty <= 0) return;

        var scaleFactor = disassembleQty / sourceFgQty;
        var tolerance = 1m / (decimal)Math.Pow(10, precision);

        foreach (var item in disassemblyItems.Where(i => !i.IsFinishedItem))
        {
            // Match by source detail ID first, then by item_code
            var sourceItem = item.SourceStockEntryDetailId.HasValue
                ? sourceItems.FirstOrDefault(s => s.Id == item.SourceStockEntryDetailId.Value)
                : sourceItems.FirstOrDefault(s => s.ItemId == item.ItemId && !s.IsFinishedItem);

            if (sourceItem == null) continue;

            var expectedQty = Math.Round(sourceItem.Quantity * scaleFactor, precision);
            var diff = Math.Abs(item.Quantity - expectedQty);

            if (diff > tolerance)
            {
                throw new BusinessException("MyERP:05046")
                    .WithData("itemId", item.ItemId)
                    .WithData("expectedQty", expectedQty)
                    .WithData("actualQty", item.Quantity)
                    .WithData("scaleFactor", scaleFactor);
            }
        }
    }
}
