using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Creates Manufacture-purpose Stock Entry from a Work Order.
/// Implements the ERPNext "Make Stock Entry" (Manufacture) workflow:
/// - Consumes raw materials from WIP warehouse (or source warehouse)
/// - Produces the finished good into FG warehouse
/// - Handles process loss deduction
/// - Validates overproduction limits
///
/// Per ERPNext stock_entry.py ManufactureStockEntry:
/// FG qty = fg_completed_qty - process_loss_qty
/// RM consumption based on BOM quantities scaled by production ratio.
/// </summary>
public class ManufactureStockEntryService : DomainService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly StockValuationService _valuationService;

    public ManufactureStockEntryService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Item, Guid> itemRepository,
        StockValuationService valuationService)
    {
        _workOrderRepository = workOrderRepository;
        _bomRepository = bomRepository;
        _itemRepository = itemRepository;
        _valuationService = valuationService;
    }

    /// <summary>
    /// Creates a Manufacture Stock Entry for a Work Order.
    /// </summary>
    /// <param name="workOrderId">Work Order to produce from.</param>
    /// <param name="fgQty">Finished goods qty to produce. Defaults to remaining WO qty.</param>
    /// <param name="overproductionPct">Max overproduction percentage from Manufacturing Settings.</param>
    /// <returns>A draft Stock Entry ready for submission.</returns>
    public async Task<StockEntry> CreateManufactureEntryAsync(
        Guid workOrderId,
        decimal? fgQty = null,
        decimal overproductionPct = 0)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId);

        if (wo.Status is not (WorkOrderStatus.InProcess or WorkOrderStatus.NotStarted or WorkOrderStatus.Submitted))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("status", wo.Status.ToString());

        var bom = await _bomRepository.GetAsync(wo.BomId);

        // Determine FG quantity to produce
        var remainingQty = wo.Quantity - wo.ProducedQuantity;
        var productionQty = fgQty ?? remainingQty;

        if (productionQty <= 0)
            throw new BusinessException("MyERP:Mfg:NothingToProduce");

        // Overproduction guard
        // Per DO-NOT: "Allow Work Order overproduction beyond configured percentage"
        var maxAllowed = wo.Quantity * (1 + overproductionPct / 100m);
        if (wo.ProducedQuantity + productionQty > maxAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", maxAllowed)
                .WithData("produced", wo.ProducedQuantity)
                .WithData("attempted", productionQty);
        }

        // Calculate process loss
        decimal processLossQty = 0;
        if (wo.ProcessLossPercentage > 0)
        {
            processLossQty = Math.Round(productionQty * wo.ProcessLossPercentage / 100m, 4);
        }
        else if (wo.ProcessLossQty > 0 && wo.Quantity > 0)
        {
            // Proportional process loss based on what fraction of WO we're producing
            processLossQty = Math.Round(wo.ProcessLossQty * (productionQty / wo.Quantity), 4);
        }

        var netFgQty = productionQty - processLossQty;

        // Resolve warehouses
        var wipWarehouse = wo.WipWarehouseId
            ?? throw new BusinessException("MyERP:Mfg:NoWipWarehouse");
        var fgWarehouse = wo.FgWarehouseId
            ?? throw new BusinessException("MyERP:Mfg:NoFgWarehouse");
        var sourceWarehouse = wo.SourceWarehouseId ?? wipWarehouse;

        // Create the Stock Entry
        var stockEntry = new StockEntry(
            GuidGenerator.Create(),
            wo.CompanyId,
            StockEntryType.Manufacture,
            DateTime.UtcNow.Date,
            wo.TenantId);

        stockEntry.WorkOrderId = wo.Id;
        stockEntry.FgCompletedQty = productionQty;
        stockEntry.ProcessLossQty = processLossQty;
        stockEntry.ProcessLossPercentage = wo.ProcessLossPercentage;

        // Scale factor: how much of each BOM component to consume
        // Per ERPNext: qty_to_manufacture / bom_quantity
        var scaleFactor = productionQty / (bom.Quantity > 0 ? bom.Quantity : 1m);

        // Add raw material consumption items (outgoing from WIP/source warehouse)
        decimal totalRmCost = 0;
        foreach (var bomItem in bom.Items)
        {
            var requiredQty = Math.Round(bomItem.Quantity * scaleFactor, 4);
            if (requiredQty <= 0) continue;

            // Get valuation rate for the raw material
            var balance = await _valuationService.GetCurrentBalanceAsync(bomItem.ItemId, sourceWarehouse);
            var valuationRate = balance.ValuationRate > 0 ? balance.ValuationRate : bomItem.Rate;

            stockEntry.AddItem(
                bomItem.ItemId,
                requiredQty,
                sourceWarehouseId: sourceWarehouse, // consume from source/WIP
                targetWarehouseId: null,            // no target — pure consumption
                valuationRate: valuationRate);

            totalRmCost += requiredQty * valuationRate;
        }

        // Add finished good item (incoming to FG warehouse)
        // Per ERPNext: FG rate = total RM cost / net FG qty (absorbed cost)
        var fgRate = netFgQty > 0 ? Math.Round(totalRmCost / netFgQty, 4) : 0;

        stockEntry.AddItem(
            wo.ItemId,
            netFgQty,
            sourceWarehouseId: null,          // no source — new production
            targetWarehouseId: fgWarehouse,   // produced into FG warehouse
            valuationRate: fgRate);

        // If process loss exists and there's a scrap warehouse, add scrap item
        if (processLossQty > 0 && wo.ScrapWarehouseId.HasValue)
        {
            stockEntry.AddItem(
                wo.ItemId,
                processLossQty,
                sourceWarehouseId: null,
                targetWarehouseId: wo.ScrapWarehouseId.Value,
                valuationRate: 0); // Process loss valued at zero per ERPNext convention
        }

        return stockEntry;
    }

    /// <summary>
    /// Creates a Material Transfer for Manufacture Stock Entry (transfer RM to WIP).
    /// This is the "Transfer Material" step before production.
    /// </summary>
    public async Task<StockEntry> CreateMaterialTransferEntryAsync(
        Guid workOrderId,
        decimal? forQty = null)
    {
        var wo = await _workOrderRepository.GetAsync(workOrderId);

        if (wo.Status is not (WorkOrderStatus.InProcess or WorkOrderStatus.NotStarted or WorkOrderStatus.Submitted))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var bom = await _bomRepository.GetAsync(wo.BomId);

        var transferQty = forQty ?? (wo.Quantity - wo.MaterialTransferred);
        if (transferQty <= 0)
            throw new BusinessException("MyERP:Mfg:NothingToTransfer");

        var sourceWarehouse = wo.SourceWarehouseId
            ?? throw new BusinessException("MyERP:Mfg:NoSourceWarehouse");
        var wipWarehouse = wo.WipWarehouseId
            ?? throw new BusinessException("MyERP:Mfg:NoWipWarehouse");

        var stockEntry = new StockEntry(
            GuidGenerator.Create(),
            wo.CompanyId,
            StockEntryType.MaterialTransferForManufacture,
            DateTime.UtcNow.Date,
            wo.TenantId);

        stockEntry.WorkOrderId = wo.Id;

        var scaleFactor = transferQty / (bom.Quantity > 0 ? bom.Quantity : 1m);

        foreach (var bomItem in bom.Items)
        {
            var requiredQty = Math.Round(bomItem.Quantity * scaleFactor, 4);
            if (requiredQty <= 0) continue;

            // Already transferred qty check (prevents excess transfer)
            // Per DO-NOT: "Allow excess material transfer for manufacture beyond required - already_transferred"
            var woItem = wo.RequiredItems.FirstOrDefault(r => r.ItemId == bomItem.ItemId);
            var alreadyTransferred = woItem?.TransferredQuantity ?? 0;
            var maxTransferable = (bomItem.Quantity * (wo.Quantity / (bom.Quantity > 0 ? bom.Quantity : 1m))) - alreadyTransferred;

            var actualTransferQty = Math.Min(requiredQty, Math.Max(0, maxTransferable));
            if (actualTransferQty <= 0) continue;

            var balance = await _valuationService.GetCurrentBalanceAsync(bomItem.ItemId, sourceWarehouse);
            var rate = balance.ValuationRate > 0 ? balance.ValuationRate : bomItem.Rate;

            stockEntry.AddItem(
                bomItem.ItemId,
                actualTransferQty,
                sourceWarehouseId: sourceWarehouse,
                targetWarehouseId: wipWarehouse,
                valuationRate: rate);
        }

        return stockEntry;
    }
}
