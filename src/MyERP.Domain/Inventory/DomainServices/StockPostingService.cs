using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Handles posting of stock transactions:
/// StockEntry → StockLedgerEntry creation → Bin updates.
/// Ensures stock movements are recorded immutably in the ledger
/// and Bin balances stay in sync.
/// </summary>
public class StockPostingService : DomainService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly BinService _binService;
    private readonly StockValuationService _valuationService;

    public StockPostingService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        BinService binService,
        StockValuationService valuationService)
    {
        _sleRepository = sleRepository;
        _companyRepository = companyRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _binService = binService;
        _valuationService = valuationService;
    }

    /// <summary>
    /// Post a stock entry — creates SLE entries for each item line and updates Bins.
    /// Validates stock frozen date before posting.
    /// </summary>
    public async Task PostStockEntryAsync(StockEntry stockEntry)
    {
        await ValidateStockFrozenDateAsync(stockEntry.CompanyId, stockEntry.PostingDate);

        foreach (var item in stockEntry.Items)
        {
            // Skip non-stock items (service items don't create SLE entries)
            var itemEntity = await _itemRepository.FindAsync(item.ItemId);
            if (itemEntity != null && !itemEntity.MaintainStock)
                continue;

            // Validate group warehouse restriction
            // Per DO-NOT: "group warehouses cannot receive stock"
            if (item.TargetWarehouseId.HasValue)
            {
                var targetWh = await _warehouseRepository.FindAsync(item.TargetWarehouseId.Value);
                if (targetWh?.IsGroup == true)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouse", targetWh.Name);
                }
            }
            if (item.SourceWarehouseId.HasValue)
            {
                var sourceWh = await _warehouseRepository.FindAsync(item.SourceWarehouseId.Value);
                if (sourceWh?.IsGroup == true)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouse", sourceWh.Name);
                }
            }

            // Source warehouse: stock-out (negative qty)
            if (item.SourceWarehouseId.HasValue)
            {
                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, item.SourceWarehouseId.Value);
                var valuationRate = balance.ValuationRate;
                var valueChange = -(item.Quantity * valuationRate);

                var sle = new StockLedgerEntry(
                    GuidGenerator.Create(), stockEntry.CompanyId,
                    item.ItemId, item.SourceWarehouseId.Value,
                    stockEntry.PostingDate, -item.Quantity,
                    valuationRate,
                    balance.Quantity - item.Quantity,
                    balance.Value + valueChange,
                    stockEntry.TenantId)
                { VoucherType = "StockEntry", VoucherId = stockEntry.Id };

                await _sleRepository.InsertAsync(sle);
                await _binService.ApplyStockMovementAsync(
                    item.ItemId, item.SourceWarehouseId.Value,
                    -item.Quantity, valueChange, stockEntry.TenantId);
            }

            // Target warehouse: stock-in (positive qty)
            if (item.TargetWarehouseId.HasValue)
            {
                var rate = item.ValuationRate ?? 0;
                var valueChange = item.Quantity * rate;

                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, item.TargetWarehouseId.Value);

                var sle = new StockLedgerEntry(
                    GuidGenerator.Create(), stockEntry.CompanyId,
                    item.ItemId, item.TargetWarehouseId.Value,
                    stockEntry.PostingDate, item.Quantity,
                    rate,
                    balance.Quantity + item.Quantity,
                    balance.Value + valueChange,
                    stockEntry.TenantId)
                { VoucherType = "StockEntry", VoucherId = stockEntry.Id };

                await _sleRepository.InsertAsync(sle);
                await _binService.ApplyStockMovementAsync(
                    item.ItemId, item.TargetWarehouseId.Value,
                    item.Quantity, valueChange, stockEntry.TenantId);
            }
        }
    }

    /// <summary>
    /// Reverse a stock posting (for cancellation).
    /// Creates opposite SLE entries and reverses Bin updates.
    /// </summary>
    public async Task ReverseStockEntryAsync(StockEntry stockEntry)
    {
        await ValidateStockFrozenDateAsync(stockEntry.CompanyId, stockEntry.PostingDate);

        foreach (var item in stockEntry.Items)
        {
            // Reverse source warehouse: add back stock
            if (item.SourceWarehouseId.HasValue)
            {
                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, item.SourceWarehouseId.Value);
                var rate = item.ValuationRate ?? 0;
                var valueChange = item.Quantity * rate;

                var sle = new StockLedgerEntry(
                    GuidGenerator.Create(), stockEntry.CompanyId,
                    item.ItemId, item.SourceWarehouseId.Value,
                    stockEntry.PostingDate, item.Quantity,
                    rate,
                    balance.Quantity + item.Quantity,
                    balance.Value + valueChange,
                    stockEntry.TenantId)
                { VoucherType = "StockEntry", VoucherId = stockEntry.Id };

                await _sleRepository.InsertAsync(sle);
                await _binService.ApplyStockMovementAsync(
                    item.ItemId, item.SourceWarehouseId.Value,
                    item.Quantity, valueChange, stockEntry.TenantId);
            }

            // Reverse target warehouse: remove stock
            if (item.TargetWarehouseId.HasValue)
            {
                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, item.TargetWarehouseId.Value);
                var rate = item.ValuationRate ?? 0;
                var valueChange = -(item.Quantity * rate);

                var sle = new StockLedgerEntry(
                    GuidGenerator.Create(), stockEntry.CompanyId,
                    item.ItemId, item.TargetWarehouseId.Value,
                    stockEntry.PostingDate, -item.Quantity,
                    rate,
                    balance.Quantity - item.Quantity,
                    balance.Value + valueChange,
                    stockEntry.TenantId)
                { VoucherType = "StockEntry", VoucherId = stockEntry.Id };

                await _sleRepository.InsertAsync(sle);
                await _binService.ApplyStockMovementAsync(
                    item.ItemId, item.TargetWarehouseId.Value,
                    -item.Quantity, valueChange, stockEntry.TenantId);
            }
        }
    }

    /// <summary>
    /// Validates that the posting date is not before the company's stock frozen date.
    /// Blocks stock transactions in frozen periods to protect closed inventory balances.
    /// </summary>
    private async Task ValidateStockFrozenDateAsync(Guid companyId, DateTime postingDate)
    {
        var company = await _companyRepository.GetAsync(companyId);
        if (company.StockFrozenUpto.HasValue && postingDate <= company.StockFrozenUpto.Value)
        {
            throw new BusinessException("MyERP:05007")
                .WithData("frozenUpto", company.StockFrozenUpto.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"));
        }
    }
}
