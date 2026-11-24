using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Propagates cost changes from child BOMs to all parent BOMs that reference them.
/// Implements the ERPNext "BOM Update Log" pattern.
/// Per DO-NOT: "Run more than one Update Cost BOM Update Log simultaneously (concurrency = 1)"
/// </summary>
public class BomCostPropagationService : DomainService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Bin, Guid>? _binRepository;
    private readonly IRepository<Warehouse, Guid>? _warehouseRepository;
    private readonly IRepository<StockLedgerEntry, Guid>? _sleRepository;
    private readonly IRepository<Item, Guid>? _itemRepository;
    private readonly IRepository<ItemPrice, Guid>? _itemPriceRepository;

    public BomCostPropagationService(IRepository<BillOfMaterials, Guid> bomRepository)
        : this(bomRepository, null, null, null, null, null)
    {
    }

    public BomCostPropagationService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Bin, Guid>? binRepository = null,
        IRepository<Warehouse, Guid>? warehouseRepository = null,
        IRepository<StockLedgerEntry, Guid>? sleRepository = null,
        IRepository<Item, Guid>? itemRepository = null,
        IRepository<ItemPrice, Guid>? itemPriceRepository = null)
    {
        _bomRepository = bomRepository;
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _itemPriceRepository = itemPriceRepository;
    }

    /// <summary>
    /// Resolves a BOM row's valuation rate per stock UOM following ERPNext bom.py:get_valuation_rate
    /// (commits ff5f2d6667 &amp; 95bf4771c3):
    /// 1) Average valuation rate from Bins in the BOM's company (scoped to <paramref name="warehouseId"/> when provided).
    /// 2) If <= 0 (including when <paramref name="warehouseId"/> has no Bin), fallback to the last non-cancelled
    ///    StockLedgerEntry with ValuationRate &gt; 0 in the BOM's own company (never another company's SLE).
    /// 3) If still <= 0, fallback to Item.StandardBuyingPrice.
    /// </summary>
    public async Task<decimal> ResolveBomItemValuationRateAsync(
        Guid itemId,
        Guid companyId,
        Guid? warehouseId = null)
    {
        decimal valuationRate = 0m;

        var binRepo = _binRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Bin, Guid>>();
        var whRepo = _warehouseRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Warehouse, Guid>>();

        if (binRepo != null)
        {
            var binQuery = await binRepo.GetQueryableAsync();
            if (binQuery != null)
            {
                if (warehouseId.HasValue)
                {
                    bool warehouseMatchesCompany = true;
                    if (whRepo != null)
                    {
                        var wh = await whRepo.FindAsync(warehouseId.Value);
                        if (wh != null && wh.CompanyId != companyId)
                        {
                            warehouseMatchesCompany = false;
                        }
                    }

                    if (warehouseMatchesCompany)
                    {
                        var scopedBins = binQuery
                            .Where(b => b.ItemId == itemId && b.WarehouseId == warehouseId.Value)
                            .ToList();
                        var totalQty = scopedBins.Sum(b => b.ActualQty);
                        var totalVal = scopedBins.Sum(b => b.StockValue);
                        if (totalQty > 0m && totalVal > 0m)
                        {
                            valuationRate = totalVal / totalQty;
                        }
                    }
                }
                else if (whRepo != null)
                {
                    var whQuery = await whRepo.GetQueryableAsync();
                    var companyWhIds = whQuery != null
                        ? whQuery.Where(w => w.CompanyId == companyId).Select(w => w.Id).ToHashSet()
                        : new HashSet<Guid>();
                    if (companyWhIds.Count > 0)
                    {
                        var companyBins = binQuery
                            .Where(b => b.ItemId == itemId && companyWhIds.Contains(b.WarehouseId))
                            .ToList();
                        var totalQty = companyBins.Sum(b => b.ActualQty);
                        var totalVal = companyBins.Sum(b => b.StockValue);
                        if (totalQty > 0m && totalVal > 0m)
                        {
                            valuationRate = totalVal / totalQty;
                        }
                    }
                }
            }
        }

        // Per ERPNext commits ff5f2d6667 & 95bf4771c3:
        // When no Bin exists (e.g. a source warehouse that never held the item) or Bin valuation <= 0,
        // fallback to the last non-cancelled SLE valuation rate scoped to the BOM's own company.
        if (valuationRate <= 0m)
        {
            var sleRepo = _sleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<StockLedgerEntry, Guid>>();
            if (sleRepo != null)
            {
                var sleQuery = await sleRepo.GetQueryableAsync();
                if (sleQuery != null)
                {
                    var lastSle = sleQuery
                        .Where(s => s.ItemId == itemId
                            && s.CompanyId == companyId
                            && s.ValuationRate > 0m
                            && !s.IsCancelled)
                        .OrderByDescending(s => s.PostingDateTime)
                        .ThenByDescending(s => s.CreationTime)
                        .FirstOrDefault();

                    if (lastSle != null)
                    {
                        valuationRate = lastSle.ValuationRate;
                    }
                }
            }
        }

        if (valuationRate <= 0m)
        {
            var itemRepo = _itemRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Item, Guid>>();
            if (itemRepo != null)
            {
                var item = await itemRepo.FindAsync(itemId);
                if (item?.StandardBuyingPrice > 0m)
                {
                    valuationRate = item.StandardBuyingPrice.Value;
                }
            }
        }

        return valuationRate;
    }

    /// <summary>
    /// Resolves a BOM row's rate from a Buying Price List as of <paramref name="asOfDate"/> (defaults to today),
    /// skipping future and expired Item Prices per ERPNext commit 62b2b6871c.
    /// </summary>
    public async Task<decimal> ResolveBomItemPriceListRateAsync(
        Guid itemId,
        Guid priceListId,
        string? uom = null,
        decimal conversionFactor = 1m,
        DateTime? asOfDate = null)
    {
        var priceRepo = _itemPriceRepository ?? LazyServiceProvider?.LazyGetService<IRepository<ItemPrice, Guid>>();
        if (priceRepo == null) return 0m;

        var priceQuery = await priceRepo.GetQueryableAsync();
        if (priceQuery == null) return 0m;

        var checkDate = (asOfDate ?? DateTime.UtcNow).Date;
        var validPrices = priceQuery
            .Where(p => p.ItemId == itemId && p.PriceListId == priceListId)
            .ToList()
            .Where(p => p.IsValidOnDate(checkDate))
            .OrderByDescending(p => p.ValidFrom ?? DateTime.MinValue)
            .ThenByDescending(p => p.CreationTime)
            .ToList();

        if (validPrices.Count == 0) return 0m;

        if (!string.IsNullOrWhiteSpace(uom))
        {
            var exactUomPrice = validPrices.FirstOrDefault(p => string.Equals(p.Uom, uom, StringComparison.OrdinalIgnoreCase));
            if (exactUomPrice != null)
            {
                return exactUomPrice.PriceListRate;
            }
        }

        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        return validPrices[0].PriceListRate * factor;
    }

    /// <summary>
    /// Updates cost of a single BOM and propagates upward to all parent BOMs.
    /// Returns the count of BOMs updated.
    /// </summary>
    public async Task<int> UpdateCostAndPropagateAsync(Guid bomId)
    {
        var bom = await _bomRepository.GetAsync(bomId, includeDetails: true);
        foreach (var item in bom.Items)
        {
            if (item.Rate <= 0m && (!item.SubBomId.HasValue || (!item.SetRateOfSubAssemblyItemBasedOnBom && !item.IsPhantom)))
            {
                var resolvedRate = await ResolveBomItemValuationRateAsync(
                    item.ItemId, bom.CompanyId, item.SourceWarehouseId ?? bom.SourceWarehouseId);
                if (resolvedRate > 0m)
                {
                    item.Rate = resolvedRate * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                }
            }
        }
        bom.RecalculateCost();
        await _bomRepository.UpdateAsync(bom);

        // Find all parent BOMs that reference this BOM's item as a sub-assembly
        var updatedCount = 1;
        var parentBoms = await FindParentBomsAsync(bomId);

        foreach (var parentBom in parentBoms)
        {
            // Update the sub-assembly item rate in the parent BOM (PR #59178: respects SetRateOfSubAssemblyItemBasedOnBom per item)
            foreach (var item in parentBom.Items.Where(i => i.SubBomId == bomId))
            {
                if (item.SetRateOfSubAssemblyItemBasedOnBom || item.IsPhantom)
                {
                    var unitCost = bom.UnitCost;
                    item.Rate = unitCost * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                    item.Recalculate();
                }
            }

            parentBom.RecalculateCost();
            await _bomRepository.UpdateAsync(parentBom);
            updatedCount++;

            // Recursively propagate upward (level-wise)
            updatedCount += await PropagateUpwardAsync(parentBom.Id);
        }

        return updatedCount;
    }

    /// <summary>
    /// Replaces every reference to <paramref name="currentBomId"/> as a sub-assembly (BomItem.SubBomId)
    /// across all parent BOMs with <paramref name="newBomId"/>, then recosts and propagates upward.
    /// Per ERPNext "BOM Update Tool" (Replace BOM): both BOMs must produce the same item.
    /// </summary>
    public async Task<(int UpdatedBomItemCount, int RecostedBomCount)> ReplaceBomAsync(Guid currentBomId, Guid newBomId)
    {
        if (currentBomId == newBomId)
            throw new BusinessException(MyERPDomainErrorCodes.BomReplaceSameBom);

        var currentBom = await _bomRepository.GetAsync(currentBomId);
        var newBom = await _bomRepository.GetAsync(newBomId);
        if (currentBom.ItemId != newBom.ItemId)
            throw new BusinessException(MyERPDomainErrorCodes.BomReplaceItemMismatch);

        // ERPNext PR #60549: replace BOM only with a submitted active BOM of same company
        if (!newBom.IsActive)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"New BOM {newBom.BomNumber} must be submitted and active.");
        }

        if (currentBom.CompanyId != newBom.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Current BOM and New BOM must have the same company.");
        }

        var parentBoms = await FindParentBomsAsync(currentBomId);

        // ERPNext PR #60549: refuse replacing BOM of parents that track semi-finished goods
        var semiFgParents = parentBoms.Where(p => p.TrackSemiFinishedGoods).ToList();
        if (semiFgParents.Count > 0)
        {
            var parentNames = string.Join(", ", semiFgParents.Select(p => p.BomNumber));
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"BOM {currentBom.BomNumber} is used in {parentNames}, which track semi-finished goods. Amend those BOMs to use the new BOM so their operation materials are fetched again.");
        }

        var newUnitCost = newBom.UnitCost;
        var updatedItemCount = 0;
        foreach (var parentBom in parentBoms)
        {
            foreach (var item in parentBom.Items.Where(i => i.SubBomId == currentBomId))
            {
                item.SubBomId = newBomId;
                if (item.SetRateOfSubAssemblyItemBasedOnBom || item.IsPhantom)
                {
                    item.Rate = newUnitCost * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                    item.Recalculate();
                }
                updatedItemCount++;
            }

            parentBom.RecalculateCost();
            await _bomRepository.UpdateAsync(parentBom);
        }

        // Propagate upward from the (now newly-linked) new BOM to recost every ancestor.
        var recostedCount = await UpdateCostAndPropagateAsync(newBomId);

        return (updatedItemCount, recostedCount);
    }

    /// <summary>
    /// Batch update: recalculates cost for all active BOMs (bottom-up, level-wise).
    /// Used by scheduled background job.
    /// </summary>
    public async Task<int> UpdateAllCostsAsync(Guid companyId)
    {
        var queryable = await _bomRepository.GetQueryableAsync();
        var allBoms = queryable
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .ToList();

        if (allBoms.Count == 0) return 0;

        var activeBomIds = allBoms.Select(b => b.Id).ToHashSet();

        // Process leaf BOMs first (no active sub-BOMs), then work upward
        var processed = new HashSet<Guid>();
        var updatedCount = 0;

        // Pass 1: BOMs with no active sub-assemblies (leaf level - ERPNext PR #60549)
        var leafBoms = allBoms.Where(b => !b.Items.Any(i => i.SubBomId.HasValue && activeBomIds.Contains(i.SubBomId.Value))).ToList();
        if (leafBoms.Count == 0)
        {
            // Complete update cost log when there are no leaf BOMs
            return 0;
        }

        foreach (var bom in leafBoms)
        {
            bom.RecalculateCost();
            await _bomRepository.UpdateAsync(bom);
            processed.Add(bom.Id);
            updatedCount++;
        }

        // Pass 2+: BOMs whose active sub-BOMs are all already processed
        var remaining = allBoms.Where(b => !processed.Contains(b.Id)).ToList();
        var maxIterations = remaining.Count + 1; // prevent infinite loop
        while (remaining.Count > 0 && maxIterations-- > 0)
        {
            var readyBoms = remaining
                .Where(b => b.Items
                    .Where(i => i.SubBomId.HasValue && activeBomIds.Contains(i.SubBomId.Value))
                    .All(i => processed.Contains(i.SubBomId!.Value)))
                .ToList();

            if (readyBoms.Count == 0) break; // Remaining have unresolvable deps (cycle)

            foreach (var bom in readyBoms)
            {
                // Update sub-assembly rates from processed child BOMs (PR #59178: respects SetRateOfSubAssemblyItemBasedOnBom per item)
                foreach (var item in bom.Items.Where(i => i.SubBomId.HasValue && activeBomIds.Contains(i.SubBomId.Value)))
                {
                    if (item.SetRateOfSubAssemblyItemBasedOnBom || item.IsPhantom)
                    {
                        var childBom = allBoms.FirstOrDefault(b => b.Id == item.SubBomId!.Value);
                        if (childBom != null)
                        {
                            var unitCost = childBom.UnitCost;
                            item.Rate = unitCost * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                            item.Recalculate();
                        }
                    }
                }

                bom.RecalculateCost();
                await _bomRepository.UpdateAsync(bom);
                processed.Add(bom.Id);
                updatedCount++;
            }

            remaining = remaining.Where(b => !processed.Contains(b.Id)).ToList();
        }

        return updatedCount;
    }

    private async Task<List<BillOfMaterials>> FindParentBomsAsync(Guid childBomId)
    {
        var queryable = await _bomRepository.GetQueryableAsync();
        return queryable
            .Where(b => b.IsActive && b.Items.Any(i => i.SubBomId == childBomId))
            .ToList();
    }

    private async Task<int> PropagateUpwardAsync(Guid bomId)
    {
        var parents = await FindParentBomsAsync(bomId);
        var count = 0;
        foreach (var parent in parents)
        {
            parent.RecalculateCost();
            await _bomRepository.UpdateAsync(parent);
            count++;
            count += await PropagateUpwardAsync(parent.Id);
        }
        return count;
    }

    /// <summary>
    /// Resolves a BOM's buying price-list conversion rate (<c>plc_conversion_rate</c>):
    /// returns <c>1</c> when price list currency equals company currency, returns <paramref name="bomConversionRate"/>
    /// when price list currency equals BOM currency, and otherwise uses <paramref name="fetchedExchangeRate"/>
    /// while preserving <paramref name="storedPlcConversionRate"/> if no exchange rate is found.
    /// Per ERPNext commits <c>324bc5ffa6</c>, <c>9ff6216212</c>, <c>ead69ffe56</c>, and <c>4768171999</c>
    /// (<c>BOM.set_plc_conversion_rate</c>).
    /// </summary>
    public static decimal ResolveBomPriceListConversionRate(
        string? priceListCurrency,
        string? companyCurrency,
        string? bomCurrency,
        decimal bomConversionRate,
        decimal? fetchedExchangeRate,
        decimal storedPlcConversionRate)
    {
        if (string.Equals(priceListCurrency, companyCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        if (string.Equals(priceListCurrency, bomCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return bomConversionRate;
        }

        return fetchedExchangeRate.HasValue && fetchedExchangeRate.Value > 0m
            ? fetchedExchangeRate.Value
            : storedPlcConversionRate;
    }

    /// <summary>
    /// Determines whether a child BOM cost update should propagate upward to parent BOMs:
    /// propagates whenever <paramref name="updateParent"/> is true and EITHER <c>TotalCost</c>
    /// or <c>BaseTotalCost</c> changed (catching foreign-currency exchange rate changes).
    /// Per ERPNext commits <c>2dde60cc73</c> and <c>42a3a8342d</c> (<c>BOMCostingService</c>).
    /// </summary>
    public static bool ShouldPropagateCostToParentBoms(
        bool updateParent,
        decimal previousTotalCost,
        decimal newTotalCost,
        decimal previousBaseTotalCost,
        decimal newBaseTotalCost)
        => updateParent && (newTotalCost != previousTotalCost || newBaseTotalCost != previousBaseTotalCost);

    /// <summary>
    /// Computes the capped process loss quantity and net-of-loss cost for a <c>ValuationRate</c>
    /// BOM secondary item row per ERPNext PR #67332 (<c>costing.py: get_secondary_process_loss_qty</c>).
    /// </summary>
    public static (decimal ProcessLossQty, decimal Cost) CalculateValuationRateSecondaryCost(
        decimal unitRate,
        decimal stockQty,
        decimal processLossPercentage,
        int lossQtyPrecision = 4,
        int costPrecision = 2)
    {
        var lossQty = Math.Min(
            Math.Round(stockQty * processLossPercentage / 100m, lossQtyPrecision, MidpointRounding.AwayFromZero),
            stockQty);
        var producedQty = stockQty - lossQty;
        var cost = Math.Round(unitRate * producedQty, costPrecision, MidpointRounding.AwayFromZero);
        return (lossQty, cost);
    }

    /// <summary>
    /// Computes fallback Valuation Rate for a Subcontracting Receipt secondary item from its BOM row's
    /// <c>BaseCost</c> divided by net produced qty (<c>stockQty - processLossQty</c>) when no stock rate exists.
    /// Per ERPNext PR #67332 (<c>subcontracting_receipt.py</c>).
    /// </summary>
    public static decimal CalculateScrFallbackSecondaryRate(
        decimal baseCost,
        decimal stockQty,
        decimal processLossQty)
    {
        var producedQty = stockQty - processLossQty;
        return producedQty > 0m ? baseCost / producedQty : 0m;
    }

    /// <summary>
    /// Resolves <c>set_cost_based_on_bom_qty</c> for a BOM Operation row per ERPNext PR #67331
    /// (<c>costing.py: set_cost_based_on_bom_qty</c>):
    /// fixed-time operations are never per-batch (<c>false</c>); operations with
    /// <c>create_job_card_based_on_batch_size</c> enabled are always per-batch (<c>true</c>);
    /// otherwise preserves <paramref name="currentFlag"/>.
    /// </summary>
    public static bool ResolveSetCostBasedOnBomQty(
        bool fixedTime,
        bool operationCreateJobCardBasedOnBatchSize,
        bool currentFlag)
    {
        if (fixedTime) return false;
        if (operationCreateJobCardBasedOnBatchSize) return true;
        return currentFlag;
    }

    /// <summary>
    /// Derives whether a BOM Operation row is costed per batch without mutating the row's stored
    /// <c>SetCostBasedOnBomQty</c> flag per ERPNext PR #69327 (<c>costing.py: is_costed_per_batch</c>):
    /// never for fixed-time operations, always when either the BOM row flag or the Operation master's
    /// <c>CreateJobCardBasedOnBatchSize</c> is enabled.
    /// </summary>
    public static bool IsCostedPerBatch(
        bool fixedTime,
        bool setCostBasedOnBomQty,
        bool operationCreateJobCardBasedOnBatchSize)
    {
        return !fixedTime && (setCostBasedOnBomQty || operationCreateJobCardBasedOnBatchSize);
    }

    /// <summary>
    /// Computes BOM secondary item <c>(Cost, BaseCost)</c> from a company-currency valuation rate
    /// per ERPNext PR #69293 (<c>bom.js: fetch_secondary_item_cost</c> / <c>bom.py: get_secondary_item_valuation_rate</c>):
    /// caps <paramref name="processLossQty"/> at <paramref name="stockQty"/>, converts the company-currency
    /// valuation rate into BOM currency using <paramref name="bomConversionRate"/>, and multiplies by net produced qty.
    /// </summary>
    public static (decimal Cost, decimal BaseCost) CalculateSecondaryItemCostFromValuationRate(
        decimal companyCurrencyValuationRate,
        decimal stockQty,
        decimal processLossQty,
        decimal bomConversionRate = 1m)
    {
        var conversionRate = bomConversionRate > 0m ? bomConversionRate : 1m;
        var cappedLoss = Math.Min(Math.Max(processLossQty, 0m), Math.Max(stockQty, 0m));
        var producedQty = Math.Max(stockQty, 0m) - cappedLoss;
        var cost = (companyCurrencyValuationRate / conversionRate) * producedQty;
        var baseCost = cost * conversionRate;
        return (cost, baseCost);
    }

    /// <summary>
    /// Resolves the updated <c>HourRate</c> on a BOM Operation row when the row's workstation/type
    /// or parent BOM's exchange conversion rate changes per ERPNext PR #67335
    /// (<c>bom/services/costing.py: _reset_row_hour_rate</c>):
    /// - Preserves manually edited rates (<c>currentHourRate != previousHourRate</c>).
    /// - Resets to <c>0</c> (triggering workstation refetch) if workstation or workstation type changed.
    /// - Rescales <c>hourRate * previousConversionRate / currentConversionRate</c> when both conversion
    ///   rates are positive and differ; skips rescaling when either conversion rate is zero/non-positive.
    /// </summary>
    public static decimal ResolveUpdatedOperationHourRate(
        decimal currentHourRate,
        decimal previousHourRate,
        Guid? previousWorkstationId,
        Guid? currentWorkstationId,
        Guid? previousWorkstationTypeId,
        Guid? currentWorkstationTypeId,
        decimal previousConversionRate,
        decimal currentConversionRate,
        int precision = 2)
    {
        if (currentHourRate != previousHourRate)
            return currentHourRate;

        if (previousWorkstationId != currentWorkstationId || previousWorkstationTypeId != currentWorkstationTypeId)
            return 0m;

        if (previousConversionRate > 0m && currentConversionRate > 0m && previousConversionRate != currentConversionRate)
        {
            return Math.Round(currentHourRate * previousConversionRate / currentConversionRate, precision, MidpointRounding.AwayFromZero);
        }

        return currentHourRate;
    }

    /// <summary>
    /// Resolves <c>plc_conversion_rate</c> for a BOM's buying Price List per ERPNext PR #67333 (<c>bom.py</c>):
    /// - Returns <c>1</c> when the price list currency matches the company currency.
    /// - Uses <paramref name="bomConversionRate"/> when the price list currency matches the BOM currency.
    /// - Otherwise (third currency) queries <paramref name="getBuyingExchangeRate"/>, keeping
    ///   <paramref name="storedPlcConversionRate"/> when no exchange rate is found.
    /// </summary>
    public static decimal ResolveBomPriceListConversionRate(
        string priceListCurrency,
        string companyCurrency,
        string bomCurrency,
        decimal bomConversionRate,
        decimal storedPlcConversionRate,
        Func<string, string, decimal>? getBuyingExchangeRate = null)
    {
        if (string.Equals(priceListCurrency, companyCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        if (string.Equals(priceListCurrency, bomCurrency, StringComparison.OrdinalIgnoreCase))
            return bomConversionRate;

        var fetched = getBuyingExchangeRate != null ? getBuyingExchangeRate(priceListCurrency, companyCurrency) : 0m;
        return fetched > 0m ? fetched : storedPlcConversionRate;
    }
}
