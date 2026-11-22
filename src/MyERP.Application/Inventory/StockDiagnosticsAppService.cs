using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockDiagnosticsAppService : ApplicationService, IStockDiagnosticsAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<StockReconciliation, Guid> _stockReconciliationRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<ItemStandardCost, Guid> _itemStandardCostRepository;
    private readonly IRepository<RepostItemValuation, Guid> _repostItemValuationRepository;
    private readonly IRepository<PeriodClosingVoucher, Guid> _periodClosingVoucherRepository;

    public StockDiagnosticsAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<StockReconciliation, Guid> stockReconciliationRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<ItemStandardCost, Guid> itemStandardCostRepository,
        IRepository<RepostItemValuation, Guid> repostItemValuationRepository,
        IRepository<PeriodClosingVoucher, Guid> periodClosingVoucherRepository)
    {
        _sleRepository = sleRepository;
        _stockReconciliationRepository = stockReconciliationRepository;
        _stockEntryRepository = stockEntryRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
        _journalEntryRepository = journalEntryRepository;
        _bundleRepository = bundleRepository;
        _serialNoRepository = serialNoRepository;
        _batchRepository = batchRepository;
        _itemGroupRepository = itemGroupRepository;
        _binRepository = binRepository;
        _itemStandardCostRepository = itemStandardCostRepository;
        _repostItemValuationRepository = repostItemValuationRepository;
        _periodClosingVoucherRepository = periodClosingVoucherRepository;
    }

    public async Task<IncorrectBalanceQtyReportDto> GetIncorrectBalanceQtyAsync(
        IncorrectBalanceQtyRequestDto input)
    {
        var precision = input.Precision > 0 ? input.Precision : 2;

        var sleQuery = (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled);

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);

        var sles = await AsyncExecuter.ToListAsync(
            sleQuery.OrderBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        if (sles.Count == 0)
            return new IncorrectBalanceQtyReportDto();

        // Pre-load StockReconciliationItem target quantities when QuantityChange == 0
        var reconVoucherIds = sles
            .Where(s => s.VoucherType == "StockReconciliation" && s.VoucherId.HasValue && s.VoucherDetailNo.HasValue)
            .Select(s => s.VoucherId!.Value)
            .Distinct()
            .ToList();

        var reconciledQtyByDetailId = new Dictionary<Guid, decimal>();
        if (reconVoucherIds.Count > 0)
        {
            var recons = await AsyncExecuter.ToListAsync(
                (await _stockReconciliationRepository.WithDetailsAsync(r => r.Items))
                .Where(r => reconVoucherIds.Contains(r.Id)));
            foreach (var item in recons.SelectMany(r => r.Items))
            {
                reconciledQtyByDetailId[item.Id] = item.NewQuantity;
            }
        }

        var mismatchedSles = new List<(StockLedgerEntry Sle, decimal ExpectedQty, decimal Difference)>();
        foreach (var group in sles.GroupBy(s => (s.ItemId, s.WarehouseId)))
        {
            decimal balanceQty = 0m;
            foreach (var sle in group)
            {
                balanceQty += sle.QuantityChange;

                if (sle.VoucherType == "StockReconciliation"
                    && !sle.BatchId.HasValue
                    && !sle.IsAdjustmentEntry)
                {
                    balanceQty = sle.BalanceQuantity;
                    if (sle.QuantityChange == 0m
                        && sle.VoucherDetailNo.HasValue
                        && reconciledQtyByDetailId.TryGetValue(sle.VoucherDetailNo.Value, out var targetQty))
                    {
                        balanceQty = targetQty;
                    }
                }

                var difference = Math.Abs(Math.Round(balanceQty - sle.BalanceQuantity, precision, MidpointRounding.AwayFromZero));
                if (difference > 0m)
                {
                    mismatchedSles.Add((sle, balanceQty, difference));
                    break;
                }
            }
        }

        if (mismatchedSles.Count == 0)
            return new IncorrectBalanceQtyReportDto();

        var itemMap = await LoadItemMapAsync(mismatchedSles.Select(m => m.Sle.ItemId));
        var whMap = await LoadWarehouseMapAsync(mismatchedSles.Select(m => m.Sle.WarehouseId));
        var voucherNoMap = await LoadVoucherNumbersAsync(mismatchedSles.Select(m => m.Sle));

        var rows = mismatchedSles.Select(m =>
        {
            itemMap.TryGetValue(m.Sle.ItemId, out var item);
            whMap.TryGetValue(m.Sle.WarehouseId, out var wh);
            return new IncorrectBalanceQtyRowDto
            {
                StockLedgerEntryId = m.Sle.Id,
                PostingDate = m.Sle.PostingDate,
                VoucherType = m.Sle.VoucherType ?? string.Empty,
                VoucherId = m.Sle.VoucherId,
                VoucherNo = ResolveVoucherNo(m.Sle, voucherNoMap),
                ItemId = m.Sle.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                WarehouseId = m.Sle.WarehouseId,
                WarehouseName = wh?.Name ?? string.Empty,
                ExpectedBalanceQty = m.ExpectedQty,
                ActualBalanceQty = m.Sle.BalanceQuantity,
                Difference = m.Difference,
            };
        }).ToList();

        return new IncorrectBalanceQtyReportDto { Rows = rows };
    }

    public async Task<IncorrectStockValueReportDto> GetIncorrectStockValueAsync(
        IncorrectStockValueRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is mandatory.");
        }

        var company = await _companyRepository.GetAsync(input.CompanyId);
        if (!company.EnablePerpetualInventory)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Perpetual inventory required for the company {company.Name} to view this report.");
        }

        if (input.AccountId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Select the stock Account to compare with the stock value.");
        }

        var stockAccountCount = await AsyncExecuter.CountAsync(
            (await _accountRepository.GetQueryableAsync())
            .Where(a => a.CompanyId == input.CompanyId
                        && a.AccountSubType == AccountSubType.Stock
                        && !a.IsGroup));

        List<Guid>? scopedWarehouseIds = null;
        if (stockAccountCount > 1)
        {
            var companyWarehouses = await _warehouseRepository.GetListAsync(w =>
                w.CompanyId == input.CompanyId && !w.IsGroup);

            scopedWarehouseIds = companyWarehouses
                .Where(w => (w.DefaultAccountId ?? company.DefaultInventoryAccountId) == input.AccountId)
                .Select(w => w.Id)
                .ToList();

            if (scopedWarehouseIds.Count == 0)
            {
                return new IncorrectStockValueReportDto
                {
                    CompanyId = input.CompanyId,
                    AccountId = input.AccountId,
                    ResolvedFromDate = input.FromDate?.Date,
                };
            }
        }

        var sleBaseQuery = (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && s.CompanyId == input.CompanyId);

        if (scopedWarehouseIds != null)
        {
            sleBaseQuery = sleBaseQuery.Where(s => scopedWarehouseIds.Contains(s.WarehouseId));
        }

        var allScopedSles = await AsyncExecuter.ToListAsync(
            sleBaseQuery.OrderBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        var resolvedFromDate = input.FromDate?.Date;
        if (!resolvedFromDate.HasValue)
        {
            resolvedFromDate = await FindFirstUnsyncDateAsync(
                input.CompanyId, input.AccountId, allScopedSles);
        }

        if (!resolvedFromDate.HasValue || allScopedSles.Count == 0)
        {
            return new IncorrectStockValueReportDto
            {
                CompanyId = input.CompanyId,
                AccountId = input.AccountId,
                ResolvedFromDate = resolvedFromDate,
            };
        }

        var fromDate = resolvedFromDate.Value;
        var runningValues = allScopedSles
            .Where(s => s.PostingDate.Date < fromDate)
            .GroupBy(s => (s.ItemId, s.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.StockValueDifference));

        var entriesFromDate = allScopedSles
            .Where(s => s.PostingDate.Date >= fromDate)
            .ToList();

        var mismatchedSles = new List<(StockLedgerEntry Sle, decimal ExpectedValue, decimal DiffValue)>();
        foreach (var sle in entriesFromDate)
        {
            var key = (sle.ItemId, sle.WarehouseId);
            var expectedStockValue = runningValues.GetValueOrDefault(key, 0m) + sle.StockValueDifference;
            var diffValue = Math.Abs(sle.BalanceValue - expectedStockValue);
            if (diffValue > 0.1m)
            {
                mismatchedSles.Add((sle, Math.Round(expectedStockValue, 2), Math.Round(diffValue, 2)));
            }

            runningValues[key] = expectedStockValue;
        }

        if (mismatchedSles.Count == 0)
        {
            return new IncorrectStockValueReportDto
            {
                CompanyId = input.CompanyId,
                AccountId = input.AccountId,
                ResolvedFromDate = resolvedFromDate,
            };
        }

        var itemMap = await LoadItemMapAsync(mismatchedSles.Select(m => m.Sle.ItemId));
        var whMap = await LoadWarehouseMapAsync(mismatchedSles.Select(m => m.Sle.WarehouseId));
        var voucherNoMap = await LoadVoucherNumbersAsync(mismatchedSles.Select(m => m.Sle));

        var rows = mismatchedSles.Select(m =>
        {
            itemMap.TryGetValue(m.Sle.ItemId, out var item);
            whMap.TryGetValue(m.Sle.WarehouseId, out var wh);
            return new IncorrectStockValueRowDto
            {
                StockLedgerEntryId = m.Sle.Id,
                PostingDate = m.Sle.PostingDate,
                PostingTime = m.Sle.PostingTime,
                VoucherType = m.Sle.VoucherType ?? string.Empty,
                VoucherId = m.Sle.VoucherId,
                VoucherNo = ResolveVoucherNo(m.Sle, voucherNoMap),
                ItemId = m.Sle.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                WarehouseId = m.Sle.WarehouseId,
                WarehouseName = wh?.Name ?? string.Empty,
                ExpectedStockValue = m.ExpectedValue,
                StockValue = m.Sle.BalanceValue,
                DifferenceValue = m.DiffValue,
            };
        }).ToList();

        return new IncorrectStockValueReportDto
        {
            CompanyId = input.CompanyId,
            AccountId = input.AccountId,
            ResolvedFromDate = resolvedFromDate,
            Rows = rows,
        };
    }

    public async Task<IncorrectSerialNoValuationReportDto> GetIncorrectSerialNoValuationAsync(
        IncorrectSerialNoValuationRequestDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }

        var fromDate = input.FromDate?.Date;
        var toExclusive = input.ToDate.HasValue ? input.ToDate.Value.Date.AddDays(1) : (DateTime?)null;

        var baseQuery = (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && (s.SerialNoId.HasValue || s.SerialAndBatchBundleId.HasValue));

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            baseQuery = baseQuery.Where(s => s.CompanyId == input.CompanyId.Value);

        List<Guid>? periodItemIds = null;
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(s => s.ItemId == input.ItemId.Value);
        }
        else if (fromDate.HasValue && toExclusive.HasValue)
        {
            periodItemIds = await AsyncExecuter.ToListAsync(
                baseQuery
                    .Where(s => s.PostingDate >= fromDate.Value && s.PostingDate < toExclusive.Value)
                    .Select(s => s.ItemId)
                    .Distinct());

            if (periodItemIds.Count == 0)
            {
                return new IncorrectSerialNoValuationReportDto
                {
                    Rows = new List<IncorrectSerialNoValuationRowDto> { CreateBalanceSummaryRow(0m, 0m) },
                };
            }

            baseQuery = baseQuery.Where(s => periodItemIds.Contains(s.ItemId));
        }

        if (toExclusive.HasValue)
            baseQuery = baseQuery.Where(s => s.PostingDate < toExclusive.Value);

        var sles = await AsyncExecuter.ToListAsync(
            baseQuery.OrderBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        var bundleIds = sles.Where(s => s.SerialAndBatchBundleId.HasValue)
            .Select(s => s.SerialAndBatchBundleId!.Value).Distinct().ToList();
        var bundleMap = bundleIds.Count > 0
            ? (await AsyncExecuter.ToListAsync(
                (await _bundleRepository.WithDetailsAsync(b => b.Entries))
                .Where(b => bundleIds.Contains(b.Id))))
                .ToDictionary(b => b.Id)
            : new Dictionary<Guid, SerialAndBatchBundle>();

        var serialIds = sles.Where(s => s.SerialNoId.HasValue)
            .Select(s => s.SerialNoId!.Value).Distinct().ToList();
        var serialMap = serialIds.Count > 0
            ? (await _serialNoRepository.GetListAsync(s => serialIds.Contains(s.Id))).ToDictionary(s => s.Id)
            : new Dictionary<Guid, SerialNo>();

        var itemMap = await LoadItemMapAsync(sles.Select(s => s.ItemId));
        var whMap = await LoadWarehouseMapAsync(sles.Select(s => s.WarehouseId));
        var voucherNoMap = await LoadVoucherNumbersAsync(sles);

        var serialTimelines = new Dictionary<string, List<(StockLedgerEntry Sle, Guid? SerialNoId, decimal Qty, decimal ValuationRate)>>(
            StringComparer.OrdinalIgnoreCase);

        void AddSerialMovement(string serialNumber, Guid? serialId, StockLedgerEntry sle, decimal qty, decimal valuationRate)
        {
            if (!serialTimelines.TryGetValue(serialNumber, out var list))
            {
                list = new List<(StockLedgerEntry, Guid?, decimal, decimal)>();
                serialTimelines[serialNumber] = list;
            }
            list.Add((sle, serialId, qty, valuationRate));
        }

        foreach (var sle in sles)
        {
            if (sle.SerialAndBatchBundleId.HasValue
                && bundleMap.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundle))
            {
                foreach (var entry in bundle.Entries.Where(e => !string.IsNullOrWhiteSpace(e.SerialNo)))
                {
                    var signedRate = entry.IncomingRate * (entry.Qty > 0m ? 1m : -1m);
                    AddSerialMovement(entry.SerialNo!, null, sle, entry.Qty, signedRate);
                }
            }
            else if (sle.SerialNoId.HasValue
                     && serialMap.TryGetValue(sle.SerialNoId.Value, out var serialObj))
            {
                var unitRate = sle.QuantityChange != 0m
                    ? sle.StockValueDifference / sle.QuantityChange
                    : sle.ValuationRate;
                var qty = sle.QuantityChange > 0m ? 1m : -1m;
                var signedRate = qty > 0m ? unitRate : -unitRate;
                AddSerialMovement(serialObj.SerialNumber, serialObj.Id, sle, qty, signedRate);
            }
        }

        var rows = new List<IncorrectSerialNoValuationRowDto>();
        decimal balanceQty = 0m;
        decimal balanceValuation = 0m;

        foreach (var (serialNumber, timeline) in serialTimelines.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            decimal totalQty = 0m;
            decimal totalVal = 0m;
            var incorrect = false;

            foreach (var movement in timeline)
            {
                totalQty += movement.Qty;
                totalVal += movement.ValuationRate;

                if (fromDate.HasValue && movement.Sle.PostingDate.Date < fromDate.Value)
                    continue;

                if ((totalQty == 0m && Math.Abs(totalVal) > 0.01m) || totalQty < 0m)
                {
                    incorrect = true;
                }
            }

            if (!incorrect)
                continue;

            foreach (var movement in timeline)
            {
                if (fromDate.HasValue && movement.Sle.PostingDate.Date < fromDate.Value)
                    continue;

                itemMap.TryGetValue(movement.Sle.ItemId, out var item);
                whMap.TryGetValue(movement.Sle.WarehouseId, out var wh);
                rows.Add(new IncorrectSerialNoValuationRowDto
                {
                    CompanyId = movement.Sle.CompanyId,
                    StockLedgerEntryId = movement.Sle.Id,
                    PostingDate = movement.Sle.PostingDate,
                    PostingTime = movement.Sle.PostingTime,
                    VoucherType = movement.Sle.VoucherType ?? string.Empty,
                    VoucherId = movement.Sle.VoucherId,
                    VoucherNo = ResolveVoucherNo(movement.Sle, voucherNoMap),
                    ItemId = movement.Sle.ItemId,
                    ItemCode = item?.ItemCode ?? string.Empty,
                    ItemName = item?.ItemName ?? string.Empty,
                    WarehouseId = movement.Sle.WarehouseId,
                    WarehouseName = wh?.Name ?? string.Empty,
                    SerialNoId = movement.SerialNoId,
                    SerialNoNumber = serialNumber,
                    Qty = movement.Qty,
                    ValuationRate = Math.Round(movement.ValuationRate, 4),
                });
            }

            balanceQty += totalQty;
            balanceValuation += totalVal;

            if (totalQty != 0m || Math.Abs(totalVal) > 0.01m)
            {
                rows.Add(new IncorrectSerialNoValuationRowDto
                {
                    SerialNoNumber = "Total",
                    Qty = totalQty,
                    ValuationRate = Math.Round(totalVal, 4),
                    IsTotalRow = true,
                });
            }
        }

        rows.Add(CreateBalanceSummaryRow(balanceQty, Math.Round(balanceValuation, 4)));
        return new IncorrectSerialNoValuationReportDto { Rows = rows };
    }

    public async Task<IncorrectSerialAndBatchBundleReportDto> GetIncorrectSerialAndBatchBundlesAsync(
        IncorrectSerialAndBatchBundleRequestDto input)
    {
        var bundleQuery = (await _bundleRepository.GetQueryableAsync()).AsQueryable();
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            bundleQuery = bundleQuery.Where(b => b.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
            bundleQuery = bundleQuery.Where(b => b.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
            bundleQuery = bundleQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(input.VoucherType))
        {
            var vt = input.VoucherType.Trim();
            bundleQuery = bundleQuery.Where(b => b.VoucherType == vt);
        }
        if (input.PostingDate.HasValue)
        {
            var start = input.PostingDate.Value.Date;
            var end = start.AddDays(1);
            bundleQuery = bundleQuery.Where(b => b.PostingDate >= start && b.PostingDate < end);
        }

        var bundles = await AsyncExecuter.ToListAsync(bundleQuery);
        if (bundles.Count == 0)
            return new IncorrectSerialAndBatchBundleReportDto();

        var bundleIds = bundles.Select(b => b.Id).ToList();
        var activeLinkedBundleIds = (await AsyncExecuter.ToListAsync(
            (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && s.SerialAndBatchBundleId.HasValue && bundleIds.Contains(s.SerialAndBatchBundleId.Value))
            .Select(s => s.SerialAndBatchBundleId!.Value)
            .Distinct()))
            .ToHashSet();

        var posVoucherIds = bundles
            .Where(b => IsPosInvoiceVoucherType(b.VoucherType))
            .Select(b => b.VoucherId)
            .Distinct()
            .ToList();
        var activePosInvoiceIds = posVoucherIds.Count > 0
            ? (await AsyncExecuter.ToListAsync(
                (await _salesInvoiceRepository.GetQueryableAsync())
                .Where(si => posVoucherIds.Contains(si.Id) && si.IsPos && si.Status == DocumentStatus.Posted)
                .Select(si => si.Id)))
                .ToHashSet()
            : new HashSet<Guid>();

        var offending = new List<(SerialAndBatchBundle Bundle, string IssueType)>();
        foreach (var bundle in bundles)
        {
            var hasActiveSle = activeLinkedBundleIds.Contains(bundle.Id);
            if (!hasActiveSle
                && bundle.Status == DocumentStatus.Submitted
                && !bundle.IsCancelled
                && !IsIgnoredBundleVoucherType(bundle.VoucherType)
                && !activePosInvoiceIds.Contains(bundle.VoucherId))
            {
                offending.Add((bundle, "UnlinkedBundle"));
            }
            else if (hasActiveSle
                     && bundle.Status == DocumentStatus.Cancelled
                     && bundle.IsCancelled)
            {
                offending.Add((bundle, "LinkedCancelledBundle"));
            }
        }

        if (offending.Count == 0)
            return new IncorrectSerialAndBatchBundleReportDto();

        var itemMap = await LoadItemMapAsync(offending.Select(o => o.Bundle.ItemId));
        var whMap = await LoadWarehouseMapAsync(offending.Select(o => o.Bundle.WarehouseId));
        var voucherNoMap = await LoadVoucherNumbersByPairsAsync(
            offending.Select(o => (o.Bundle.VoucherType, o.Bundle.VoucherId)));

        var rows = offending.Select(o =>
        {
            var b = o.Bundle;
            itemMap.TryGetValue(b.ItemId, out var item);
            whMap.TryGetValue(b.WarehouseId, out var wh);
            var no = voucherNoMap.TryGetValue((b.VoucherType, b.VoucherId), out var resolvedNo)
                ? resolvedNo
                : b.VoucherId.ToString()[..8];
            return new IncorrectSerialAndBatchBundleRowDto
            {
                BundleId = b.Id,
                CompanyId = b.CompanyId,
                ItemId = b.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                WarehouseId = b.WarehouseId,
                WarehouseName = wh?.Name ?? string.Empty,
                VoucherType = b.VoucherType,
                VoucherId = b.VoucherId,
                VoucherNo = no,
                VoucherDetailId = b.VoucherDetailId,
                IsCancelled = b.IsCancelled,
                IssueType = o.IssueType,
            };
        }).OrderBy(r => r.VoucherType).ThenBy(r => r.VoucherNo).ToList();

        return new IncorrectSerialAndBatchBundleReportDto { Rows = rows };
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<FixSerialAndBatchBundlesResultDto> FixIncorrectSerialAndBatchBundlesAsync(
        FixSerialAndBatchBundlesInputDto input)
    {
        var result = new FixSerialAndBatchBundlesResultDto();
        if (input.BundleIds == null || input.BundleIds.Count == 0)
            return result;

        var ids = input.BundleIds.Distinct().ToList();
        var bundles = await _bundleRepository.GetListAsync(b => ids.Contains(b.Id));
        var activeLinkedIds = (await AsyncExecuter.ToListAsync(
            (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && s.SerialAndBatchBundleId.HasValue && ids.Contains(s.SerialAndBatchBundleId.Value))
            .Select(s => s.SerialAndBatchBundleId!.Value)
            .Distinct()))
            .ToHashSet();

        foreach (var bundle in bundles)
        {
            var hasActiveSle = activeLinkedIds.Contains(bundle.Id);
            if (bundle.Status == DocumentStatus.Submitted && !bundle.IsCancelled && !hasActiveSle)
            {
                if (IsPosInvoiceVoucherType(bundle.VoucherType))
                {
                    var posInv = await _salesInvoiceRepository.FindAsync(bundle.VoucherId);
                    if (posInv is { IsPos: true, Status: DocumentStatus.Posted })
                    {
                        result.SkippedCount++;
                        continue;
                    }
                }

                bundle.Cancel();
                await _bundleRepository.UpdateAsync(bundle);
                result.CancelledCount++;
            }
            else if (bundle.Status == DocumentStatus.Cancelled && bundle.IsCancelled && hasActiveSle)
            {
                bundle.RestoreSubmitted();
                await _bundleRepository.UpdateAsync(bundle);
                result.RestoredCount++;
            }
            else
            {
                result.SkippedCount++;
            }
        }

        return result;
    }

    public async Task<FifoQueueVsQtyComparisonReportDto> GetFifoQueueVsQtyComparisonAsync(
        FifoQueueVsQtyComparisonRequestDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }

        if (!input.WarehouseId.HasValue && !input.ItemId.HasValue && !input.ItemGroupId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Any one of following filters required: Warehouse, Item, Item Group.");
        }

        var sleQuery = (await _sleRepository.GetQueryableAsync()).Where(s => !s.IsCancelled);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);

        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            var descendantWarehouseIds = await ExpandDescendantWarehouseIdsAsync(input.WarehouseId.Value);
            sleQuery = sleQuery.Where(s => descendantWarehouseIds.Contains(s.WarehouseId));
        }

        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);
        }
        else if (input.ItemGroupId.HasValue && input.ItemGroupId.Value != Guid.Empty)
        {
            var matchingItemIds = await GetItemIdsForGroupTreeAsync(input.ItemGroupId.Value);
            if (matchingItemIds.Count == 0)
                return new FifoQueueVsQtyComparisonReportDto();

            sleQuery = sleQuery.Where(s => matchingItemIds.Contains(s.ItemId));
        }

        if (input.FromDate.HasValue)
        {
            var fromStart = input.FromDate.Value.Date;
            sleQuery = sleQuery.Where(s => s.PostingDate >= fromStart);
        }

        if (input.ToDate.HasValue)
        {
            var toExclusive = input.ToDate.Value.Date.AddDays(1);
            sleQuery = sleQuery.Where(s => s.PostingDate < toExclusive);
        }

        var sles = await AsyncExecuter.ToListAsync(
            sleQuery.OrderBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        if (sles.Count == 0)
            return new FifoQueueVsQtyComparisonReportDto();

        var itemMap = await LoadItemMapAsync(sles.Select(s => s.ItemId));
        var whMap = await LoadWarehouseMapAsync(sles.Select(s => s.WarehouseId));
        var batchIds = sles.Where(s => s.BatchId.HasValue).Select(s => s.BatchId!.Value).Distinct().ToList();
        var batchMap = batchIds.Count > 0
            ? (await _batchRepository.GetListAsync(b => batchIds.Contains(b.Id))).ToDictionary(b => b.Id)
            : new Dictionary<Guid, Batch>();

        var selectedPairs = new List<(StockLedgerEntry Sle, bool UseBatchwise, decimal FifoQty, decimal FifoQtyDiff, decimal FifoVal, decimal FifoValDiff, bool IsPreceding)>();

        foreach (var group in sles.GroupBy(s => (s.ItemId, s.WarehouseId)))
        {
            if (!itemMap.TryGetValue(group.Key.ItemId, out var item))
                continue;

            var isFifoOrLifo = item.ValuationMethod == ValuationMethod.FIFO || item.ValuationMethod == ValuationMethod.LIFO;
            if (!isFifoOrLifo)
                continue;

            var isLifo = item.ValuationMethod == ValuationMethod.LIFO;
            var groupEntries = group.ToList();

            (decimal FifoQty, decimal FifoQtyDiff, decimal FifoVal, decimal FifoValDiff, bool UseBatchwise) Evaluate(StockLedgerEntry e)
            {
                batchMap.TryGetValue(e.BatchId ?? Guid.Empty, out var batch);
                var useBatchwise = batch?.UseBatchwiseValuation ?? false;
                var queue = FifoValuation.Deserialize(e.StockQueue, isLifo);
                var qQty = queue.TotalQty;
                var qVal = queue.TotalValue;
                return (qQty, e.BalanceQuantity - qQty, qVal, e.BalanceValue - qVal, useBatchwise);
            }

            for (var idx = 0; idx < groupEntries.Count; idx++)
            {
                var sle = groupEntries[idx];
                var useBatchwise = sle.BatchId.HasValue
                    && batchMap.TryGetValue(sle.BatchId.Value, out var b)
                    && b.UseBatchwiseValuation;

                var keepsQueue = (sle.HasSerialNo && !item.UseSerialNoWiseValuation)
                    || (!sle.HasSerialNo && sle.SerialAndBatchBundleId == null && !(sle.HasBatchNo && useBatchwise));

                if (!keepsQueue)
                    continue;

                var metrics = Evaluate(sle);
                if (Math.Abs(metrics.FifoQtyDiff) > 0.001m || Math.Abs(metrics.FifoValDiff) > 0.1m)
                {
                    if (idx > 0)
                    {
                        var prev = groupEntries[idx - 1];
                        var prevMetrics = Evaluate(prev);
                        selectedPairs.Add((prev, prevMetrics.UseBatchwise, prevMetrics.FifoQty, prevMetrics.FifoQtyDiff, prevMetrics.FifoVal, prevMetrics.FifoValDiff, true));
                    }

                    selectedPairs.Add((sle, metrics.UseBatchwise, metrics.FifoQty, metrics.FifoQtyDiff, metrics.FifoVal, metrics.FifoValDiff, false));
                    break;
                }
            }
        }

        if (selectedPairs.Count == 0)
            return new FifoQueueVsQtyComparisonReportDto();

        var voucherNoMap = await LoadVoucherNumbersAsync(selectedPairs.Select(p => p.Sle));
        var rows = selectedPairs.Select(p =>
        {
            itemMap.TryGetValue(p.Sle.ItemId, out var item);
            whMap.TryGetValue(p.Sle.WarehouseId, out var wh);
            return new FifoQueueVsQtyComparisonRowDto
            {
                StockLedgerEntryId = p.Sle.Id,
                ItemId = p.Sle.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                WarehouseId = p.Sle.WarehouseId,
                WarehouseName = wh?.Name ?? string.Empty,
                PostingDate = p.Sle.PostingDate,
                PostingTime = p.Sle.PostingTime,
                VoucherType = p.Sle.VoucherType ?? string.Empty,
                VoucherId = p.Sle.VoucherId,
                VoucherNo = ResolveVoucherNo(p.Sle, voucherNoMap),
                BatchId = p.Sle.BatchId,
                UseBatchwiseValuation = p.UseBatchwise,
                ActualQty = p.Sle.QuantityChange,
                QtyAfterTransaction = p.Sle.BalanceQuantity,
                StockQueue = p.Sle.StockQueue ?? "[]",
                FifoQueueQty = Math.Round(p.FifoQty, 4),
                FifoQtyDiff = Math.Round(p.FifoQtyDiff, 4),
                StockValue = p.Sle.BalanceValue,
                FifoStockValue = Math.Round(p.FifoVal, 2),
                FifoValueDiff = Math.Round(p.FifoValDiff, 2),
                ValuationRate = p.Sle.ValuationRate,
                IsPrecedingRow = p.IsPreceding,
            };
        }).ToList();

        return new FifoQueueVsQtyComparisonReportDto { Rows = rows };
    }

    public async Task<StockValuationComparisonReportDto> GetStockValuationComparisonAsync(
        StockValuationComparisonRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select a Company.");
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange)
                .WithData("detail", "From Date cannot be after To Date.");
        }

        var showMode = string.IsNullOrWhiteSpace(input.Show)
            ? StockValuationComparisonShowModes.FirstDifferencePerItemWarehouse
            : input.Show.Trim();
        var tolerance = input.Tolerance ?? 0.01m;
        var effectiveTolerance = Math.Max(tolerance, ExpectedValuationReplayEngine.QtyNoise);

        await _companyRepository.GetAsync(input.CompanyId);
        var doNotUseBatchwise = await SettingProvider.IsTrueAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation);

        var companyWarehouses = await _warehouseRepository.GetListAsync(w => w.CompanyId == input.CompanyId);
        var whMap = companyWarehouses.ToDictionary(w => w.Id);

        HashSet<Guid>? allowedWarehouseIds = null;
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            allowedWarehouseIds = (await ExpandDescendantWarehouseIdsAsync(input.WarehouseId.Value)).ToHashSet();
        }
        else
        {
            allowedWarehouseIds = whMap.Keys.ToHashSet();
        }

        HashSet<Guid>? allowedItemIds = null;
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            allowedItemIds = new HashSet<Guid> { input.ItemId.Value };
        }
        else if (input.ItemGroupId.HasValue && input.ItemGroupId.Value != Guid.Empty)
        {
            allowedItemIds = (await GetItemIdsForGroupTreeAsync(input.ItemGroupId.Value)).ToHashSet();
            if (allowedItemIds.Count == 0)
            {
                return new StockValuationComparisonReportDto
                {
                    CompanyId = input.CompanyId,
                    Tolerance = tolerance,
                    Show = showMode,
                };
            }
        }

        var sleQuery = (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && s.CompanyId == input.CompanyId);

        if (allowedWarehouseIds != null)
        {
            var whFilter = allowedWarehouseIds.ToList();
            sleQuery = sleQuery.Where(s => whFilter.Contains(s.WarehouseId));
        }

        if (allowedItemIds != null)
        {
            var itemFilter = allowedItemIds.ToList();
            sleQuery = sleQuery.Where(s => itemFilter.Contains(s.ItemId));
        }

        if (input.ToDate.HasValue)
        {
            var end = input.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            sleQuery = sleQuery.Where(s => s.PostingDate <= end);
        }

        var allSles = await AsyncExecuter.ToListAsync(
            sleQuery.OrderBy(s => s.ItemId).ThenBy(s => s.WarehouseId)
                .ThenBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        if (allSles.Count == 0)
        {
            return new StockValuationComparisonReportDto
            {
                CompanyId = input.CompanyId,
                Tolerance = tolerance,
                Show = showMode,
            };
        }

        var sleItemIds = allSles.Select(s => s.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetListAsync(i => sleItemIds.Contains(i.Id) && i.MaintainStock))
            .ToDictionary(i => i.Id);

        var binPairs = (await AsyncExecuter.ToListAsync(
            (await _binRepository.GetQueryableAsync())
            .Where(b => sleItemIds.Contains(b.ItemId))
            .Select(b => new { b.ItemId, b.WarehouseId })))
            .Select(b => (b.ItemId, b.WarehouseId));

        var itemWarehousePairs = allSles
            .Where(s => itemMap.ContainsKey(s.ItemId))
            .Select(s => (s.ItemId, s.WarehouseId))
            .Concat(binPairs.Where(p => itemMap.ContainsKey(p.ItemId)
                                        && (allowedWarehouseIds == null || allowedWarehouseIds.Contains(p.WarehouseId))))
            .Distinct()
            .OrderBy(p => itemMap[p.ItemId].ItemCode)
            .ThenBy(p => whMap.TryGetValue(p.WarehouseId, out var w) ? w.Name : string.Empty)
            .ToList();

        // Bulk load auxiliary replay inputs
        var bundleIds = allSles.Where(s => s.SerialAndBatchBundleId.HasValue).Select(s => s.SerialAndBatchBundleId!.Value).Distinct().ToList();
        var bundleMap = bundleIds.Count > 0
            ? (await AsyncExecuter.ToListAsync(
                (await _bundleRepository.WithDetailsAsync(b => b.Entries))
                .Where(b => bundleIds.Contains(b.Id))))
                .ToDictionary(b => b.Id)
            : new Dictionary<Guid, SerialAndBatchBundle>();

        var allBatchIds = allSles.Where(s => s.BatchId.HasValue).Select(s => s.BatchId!.Value)
            .Concat(bundleMap.Values.SelectMany(b => b.Entries).Where(e => e.BatchId.HasValue).Select(e => e.BatchId!.Value))
            .Distinct()
            .ToList();
        var batchMap = allBatchIds.Count > 0
            ? (await _batchRepository.GetListAsync(b => allBatchIds.Contains(b.Id))).ToDictionary(b => b.Id)
            : new Dictionary<Guid, Batch>();
        var batchwiseBatchIds = batchMap.Values.Where(b => b.UseBatchwiseValuation).Select(b => b.Id).ToHashSet();

        var serialIds = allSles.Where(s => s.SerialNoId.HasValue).Select(s => s.SerialNoId!.Value).Distinct().ToList();
        var serialMap = serialIds.Count > 0
            ? (await _serialNoRepository.GetListAsync(sn => serialIds.Contains(sn.Id))).ToDictionary(sn => sn.Id)
            : new Dictionary<Guid, SerialNo>();

        var reconIds = allSles
            .Where(s => s.VoucherId.HasValue && IsStockReconciliationVoucher(s.VoucherType))
            .Select(s => s.VoucherId!.Value)
            .Distinct()
            .ToList();
        var reconMap = reconIds.Count > 0
            ? (await AsyncExecuter.ToListAsync(
                (await _stockReconciliationRepository.WithDetailsAsync(r => r.Items))
                .Where(r => reconIds.Contains(r.Id))))
                .ToDictionary(r => r.Id)
            : new Dictionary<Guid, StockReconciliation>();

        var reconItemById = reconMap.Values
            .SelectMany(r => r.Items)
            .ToDictionary(i => i.Id);

        var adjustmentVoucherIds = reconMap.Values
            .Where(r => string.Equals(r.Purpose, ExpectedValuationReplayEngine.AdjustmentEntryPurpose, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Id)
            .ToHashSet();

        // Latest adjustment entry cutoff per (Item, Warehouse)
        var adjustedUntilMap = BuildAdjustedUntilMap(reconMap.Values, adjustmentVoucherIds, input.ToDate);

        // Pending RepostItemValuation per (Item, Warehouse)
        var pendingReposts = await LoadPendingItemWarehouseRepostMapAsync(input.CompanyId, sleItemIds);

        // Purchase return rates (for FIFO and LIFO purchase returns, ERPNext PR #67376)
        var purchaseReturnRates = await LoadPurchaseReturnRateMapAsync(allSles);

        // Standard Cost schedule per Item
        var standardCostItems = itemMap.Values
            .Where(i => i.ValuationMethod == ValuationMethod.StandardCost)
            .Select(i => i.Id)
            .ToList();
        var standardCostMap = standardCostItems.Count > 0
            ? (await AsyncExecuter.ToListAsync(
                (await _itemStandardCostRepository.GetQueryableAsync())
                .Where(sc => sc.CompanyId == input.CompanyId
                             && sc.Status == DocumentStatus.Submitted
                             && standardCostItems.Contains(sc.ItemId))
                .OrderBy(sc => sc.EffectiveDate).ThenBy(sc => sc.CreationTime)))
                .GroupBy(sc => sc.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<Guid, List<ItemStandardCost>>();

        var voucherNoMap = await LoadVoucherNumbersAsync(allSles);
        var slesByPair = allSles.GroupBy(s => (s.ItemId, s.WarehouseId)).ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<StockValuationComparisonRowDto>();
        var totalEvaluated = 0;
        var fromDate = input.FromDate?.Date;

        foreach (var pair in itemWarehousePairs)
        {
            if (!slesByPair.TryGetValue(pair, out var pairSles) || pairSles.Count == 0)
                continue;

            var item = itemMap[pair.ItemId];
            whMap.TryGetValue(pair.WarehouseId, out var wh);
            pendingReposts.TryGetValue(pair, out var pendingRepostId);
            adjustedUntilMap.TryGetValue(pair, out var adjustedUntil);

            var lastAdjustmentSleIds = pairSles
                .Where(s => s.VoucherId.HasValue
                            && IsStockReconciliationVoucher(s.VoucherType)
                            && adjustmentVoucherIds.Contains(s.VoucherId.Value))
                .GroupBy(s => s.VoucherId!.Value)
                .Select(g => g.Last().Id)
                .ToHashSet();

            var inputs = pairSles.Select(sle => BuildReplayEntryInput(
                sle,
                item,
                bundleMap,
                serialMap,
                reconMap,
                reconItemById,
                adjustmentVoucherIds,
                lastAdjustmentSleIds,
                purchaseReturnRates,
                standardCostMap)).ToList();

            var context = new ExpectedValuationReplayEngine.ItemReplayContext(
                item.ValuationMethod,
                item.HasSerialNo,
                item.HasBatchNo,
                item.UseSerialNoWiseValuation,
                doNotUseBatchwise,
                2,
                0.001m,
                0.01m,
                batchwiseBatchIds);

            var replayed = ExpectedValuationReplayEngine.ReplayLedger(inputs, context);

            foreach (var (entry, expected) in replayed)
            {
                totalEvaluated++;
                if (fromDate.HasValue && entry.PostingDate.Date < fromDate.Value)
                    continue;

                var originalSle = pairSles.First(s => s.Id == entry.SleId);
                var qtyDiff = ApplyTolerance(originalSle.BalanceQuantity - expected.QtyAfterTransaction, effectiveTolerance);
                var diffChange = ApplyTolerance(originalSle.StockValueDifference - expected.StockValueDifference, effectiveTolerance);
                var rateDiff = expected.QtyAfterTransaction == 0m
                    ? 0m
                    : ApplyTolerance(originalSle.ValuationRate - expected.ValuationRate, effectiveTolerance);
                var balValDiff = ApplyTolerance(originalSle.BalanceValue - expected.StockValue, effectiveTolerance);

                var hasDiff = qtyDiff != 0m || diffChange != 0m || rateDiff != 0m || balValDiff != 0m;
                string? adjustedBy = null;

                if (hasDiff && adjustedUntil != null && entry.PostingDateTime < adjustedUntil.Value.PostingDateTime)
                {
                    adjustedBy = adjustedUntil.Value.VoucherNo;
                    if (!input.ShowAdjustedDifferences)
                    {
                        hasDiff = false;
                        if (showMode == StockValuationComparisonShowModes.AllEntries)
                        {
                            qtyDiff = 0m;
                            diffChange = 0m;
                            rateDiff = 0m;
                            balValDiff = 0m;
                        }
                    }
                }

                if (showMode == StockValuationComparisonShowModes.AllEntries || hasDiff)
                {
                    batchMap.TryGetValue(originalSle.BatchId ?? Guid.Empty, out var batch);
                    rows.Add(new StockValuationComparisonRowDto
                    {
                        StockLedgerEntryId = originalSle.Id,
                        PostingDate = originalSle.PostingDate,
                        PostingTime = originalSle.PostingTime,
                        PostingDateTime = originalSle.PostingDateTime,
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        WarehouseId = originalSle.WarehouseId,
                        WarehouseName = wh?.Name ?? string.Empty,
                        VoucherType = originalSle.VoucherType ?? string.Empty,
                        VoucherId = originalSle.VoucherId,
                        VoucherNo = ResolveVoucherNo(originalSle, voucherNoMap),
                        SerialAndBatchBundleId = originalSle.SerialAndBatchBundleId,
                        BatchId = originalSle.BatchId,
                        BatchNo = batch?.BatchNo,
                        ActualQty = originalSle.QuantityChange,
                        QtyAfterTransaction = originalSle.BalanceQuantity,
                        ExpectedQtyAfterTransaction = Math.Round(expected.QtyAfterTransaction, 4),
                        QtyDifference = Math.Round(qtyDiff, 4),
                        StockValueDifference = originalSle.StockValueDifference,
                        ExpectedStockValueDifference = Math.Round(expected.StockValueDifference, 2),
                        StockValueDifferenceDifference = Math.Round(diffChange, 2),
                        ValuationRate = originalSle.ValuationRate,
                        ExpectedValuationRate = Math.Round(expected.ValuationRate, 4),
                        ValuationRateDifference = Math.Round(rateDiff, 4),
                        StockValue = originalSle.BalanceValue,
                        ExpectedStockValue = Math.Round(expected.StockValue, 2),
                        StockValueDifferenceInBalance = Math.Round(balValDiff, 2),
                        ExpectedBasis = expected.Basis,
                        HasDifference = hasDiff,
                        AdjustedBy = adjustedBy,
                        PendingRepostId = pendingRepostId == Guid.Empty ? null : pendingRepostId,
                    });
                }

                if (showMode == StockValuationComparisonShowModes.FirstDifferencePerItemWarehouse && hasDiff)
                    break;
            }
        }

        return new StockValuationComparisonReportDto
        {
            CompanyId = input.CompanyId,
            Tolerance = tolerance,
            Show = showMode,
            TotalEntriesEvaluated = totalEvaluated,
            DifferenceCount = rows.Count(r => r.HasDifference),
            Rows = rows,
        };
    }

    public async Task<List<StockValuationComparisonRepostPreviewRowDto>> GetStockValuationRepostPreviewAsync(
        StockValuationComparisonRepostRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select a Company.");
        }

        if (input.StockLedgerEntryIds == null || input.StockLedgerEntryIds.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Select the differences to fix.");
        }

        var sleIds = input.StockLedgerEntryIds.Distinct().ToList();
        var sles = await AsyncExecuter.ToListAsync(
            (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && s.CompanyId == input.CompanyId && sleIds.Contains(s.Id))
            .OrderBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        var earliestPerPair = new Dictionary<(Guid ItemId, Guid WarehouseId), StockLedgerEntry>();
        foreach (var sle in sles)
        {
            earliestPerPair.TryAdd((sle.ItemId, sle.WarehouseId), sle);
        }

        if (earliestPerPair.Count == 0)
            return new List<StockValuationComparisonRepostPreviewRowDto>();

        var itemMap = await LoadItemMapAsync(earliestPerPair.Keys.Select(k => k.ItemId));
        var whMap = await LoadWarehouseMapAsync(earliestPerPair.Keys.Select(k => k.WarehouseId));
        var pendingReposts = await LoadPendingItemWarehouseRepostMapAsync(
            input.CompanyId, earliestPerPair.Keys.Select(k => k.ItemId));

        var maxClosedDate = await AsyncExecuter.FirstOrDefaultAsync(
            (await _periodClosingVoucherRepository.GetQueryableAsync())
            .Where(p => p.CompanyId == input.CompanyId && p.Status == DocumentStatus.Submitted)
            .OrderByDescending(p => p.PostingDate)
            .Select(p => (DateTime?)p.PostingDate));

        return earliestPerPair.Values.Select(sle =>
        {
            itemMap.TryGetValue(sle.ItemId, out var item);
            whMap.TryGetValue(sle.WarehouseId, out var wh);
            pendingReposts.TryGetValue((sle.ItemId, sle.WarehouseId), out var pendingId);
            return new StockValuationComparisonRepostPreviewRowDto
            {
                StockLedgerEntryId = sle.Id,
                ItemId = sle.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                WarehouseId = sle.WarehouseId,
                WarehouseName = wh?.Name ?? string.Empty,
                PostingDate = sle.PostingDate,
                PostingTime = sle.PostingTime,
                IsInClosedPeriod = maxClosedDate.HasValue && sle.PostingDate.Date <= maxClosedDate.Value.Date,
                PendingRepostId = pendingId == Guid.Empty ? null : pendingId,
            };
        }).ToList();
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockValuationComparisonRepostResultDto> CreateStockValuationRepostsAsync(
        StockValuationComparisonRepostRequestDto input)
    {
        var previewRows = await GetStockValuationRepostPreviewAsync(input);
        var result = new StockValuationComparisonRepostResultDto();

        foreach (var row in previewRows)
        {
            if (row.PendingRepostId.HasValue)
            {
                result.Skipped.Add(new StockValuationComparisonRepostItemResultDto
                {
                    ItemId = row.ItemId,
                    ItemCode = row.ItemCode,
                    WarehouseId = row.WarehouseId,
                    WarehouseName = row.WarehouseName,
                    PostingDate = row.PostingDate,
                    RepostId = row.PendingRepostId,
                    Reason = $"Repost {row.PendingRepostId.Value.ToString()[..8]} is already pending.",
                });
                continue;
            }

            if (row.IsInClosedPeriod)
            {
                result.Skipped.Add(new StockValuationComparisonRepostItemResultDto
                {
                    ItemId = row.ItemId,
                    ItemCode = row.ItemCode,
                    WarehouseId = row.WarehouseId,
                    WarehouseName = row.WarehouseName,
                    PostingDate = row.PostingDate,
                    Reason = $"Posting date {row.PostingDate:yyyy-MM-dd} falls in a closed accounting period.",
                });
                continue;
            }

            var repost = new RepostItemValuation(
                GuidGenerator.Create(),
                input.CompanyId,
                RepostMethod.ItemAndWarehouse,
                row.PostingDate,
                row.ItemId,
                row.WarehouseId,
                CurrentTenant.Id)
            {
                PostingTime = row.PostingTime,
                RepostGlEntries = true,
            };

            await _repostItemValuationRepository.InsertAsync(repost, autoSave: true);
            result.Created.Add(new StockValuationComparisonRepostItemResultDto
            {
                ItemId = row.ItemId,
                ItemCode = row.ItemCode,
                WarehouseId = row.WarehouseId,
                WarehouseName = row.WarehouseName,
                PostingDate = row.PostingDate,
                RepostId = repost.Id,
            });
        }

        return result;
    }

    private static decimal ApplyTolerance(decimal diff, decimal tolerance)
        => Math.Abs(diff) < tolerance ? 0m : diff;

    private static bool IsStockReconciliationVoucher(string? voucherType)
        => !string.IsNullOrWhiteSpace(voucherType)
           && string.Equals(voucherType.Replace(" ", string.Empty), "StockReconciliation", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<(Guid ItemId, Guid WarehouseId), (DateTime PostingDateTime, string VoucherNo)?> BuildAdjustedUntilMap(
        IEnumerable<StockReconciliation> reconciliations,
        HashSet<Guid> adjustmentVoucherIds,
        DateTime? toDate)
    {
        var map = new Dictionary<(Guid ItemId, Guid WarehouseId), (DateTime PostingDateTime, string VoucherNo)?>();
        foreach (var rec in reconciliations.Where(r => adjustmentVoucherIds.Contains(r.Id)
                                                       && r.Status == DocumentStatus.Submitted
                                                       && (!toDate.HasValue || r.PostingDate.Date <= toDate.Value.Date)))
        {
            var voucherNo = rec.ReconciliationNumber ?? rec.Id.ToString()[..8];
            foreach (var row in rec.Items)
            {
                var key = (row.ItemId, row.WarehouseId);
                if (!map.TryGetValue(key, out var existing) || rec.PostingDate > existing!.Value.PostingDateTime)
                {
                    map[key] = (rec.PostingDate, voucherNo);
                }
            }
        }
        return map;
    }

    private async Task<Dictionary<(Guid ItemId, Guid WarehouseId), Guid>> LoadPendingItemWarehouseRepostMapAsync(
        Guid companyId,
        IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<(Guid, Guid), Guid>();

        var pending = (await AsyncExecuter.ToListAsync(
            (await _repostItemValuationRepository.GetQueryableAsync())
            .Where(r => r.CompanyId == companyId
                        && r.BasedOn == RepostMethod.ItemAndWarehouse
                        && (r.Status == RepostStatus.Queued || r.Status == RepostStatus.InProgress)
                        && r.ItemId.HasValue
                        && r.WarehouseId.HasValue
                        && ids.Contains(r.ItemId.Value))))
            .OrderBy(r => r.PostingDate)
            .ThenBy(r => r.PostingTime)
            .ToList();

        var map = new Dictionary<(Guid, Guid), Guid>();
        foreach (var r in pending)
        {
            map.TryAdd((r.ItemId!.Value, r.WarehouseId!.Value), r.Id);
        }
        return map;
    }

    private async Task<Dictionary<Guid, decimal>> LoadPurchaseReturnRateMapAsync(List<StockLedgerEntry> sles)
    {
        var result = new Dictionary<Guid, decimal>();
        var prOut = sles.Where(s => s.QuantityChange < 0m
                                    && s.VoucherId.HasValue
                                    && string.Equals(s.VoucherType?.Replace(" ", string.Empty), "PurchaseReceipt", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var piOut = sles.Where(s => s.QuantityChange < 0m
                                    && s.VoucherId.HasValue
                                    && string.Equals(s.VoucherType?.Replace(" ", string.Empty), "PurchaseInvoice", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var returnedSourceByVoucher = new Dictionary<(string NormalizedType, Guid ReturnVoucherId), Guid>();

        if (prOut.Count > 0)
        {
            var prIds = prOut.Select(s => s.VoucherId!.Value).Distinct().ToList();
            foreach (var pr in await _purchaseReceiptRepository.GetListAsync(r => prIds.Contains(r.Id) && r.IsReturn && r.ReturnAgainstId.HasValue))
            {
                returnedSourceByVoucher[("PURCHASERECEIPT", pr.Id)] = pr.ReturnAgainstId!.Value;
            }
        }

        if (piOut.Count > 0)
        {
            var piIds = piOut.Select(s => s.VoucherId!.Value).Distinct().ToList();
            foreach (var pi in await _purchaseInvoiceRepository.GetListAsync(i => piIds.Contains(i.Id) && i.IsReturn && i.ReturnAgainstId.HasValue))
            {
                returnedSourceByVoucher[("PURCHASEINVOICE", pi.Id)] = pi.ReturnAgainstId!.Value;
            }
        }

        if (returnedSourceByVoucher.Count == 0)
            return result;

        var sourceVoucherIds = returnedSourceByVoucher.Values.Distinct().ToList();
        var receiptSles = await AsyncExecuter.ToListAsync(
            (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled
                        && s.QuantityChange > 0m
                        && s.VoucherId.HasValue
                        && sourceVoucherIds.Contains(s.VoucherId.Value))
            .OrderBy(s => s.PostingDateTime).ThenBy(s => s.CreationTime));

        var receiptRateMap = new Dictionary<(string NormalizedType, Guid VoucherId, Guid ItemId, Guid WarehouseId), decimal>();
        foreach (var rs in receiptSles)
        {
            var norm = (rs.VoucherType ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
            receiptRateMap.TryAdd((norm, rs.VoucherId!.Value, rs.ItemId, rs.WarehouseId), rs.IncomingRate);
        }

        foreach (var sle in prOut.Concat(piOut))
        {
            var norm = (sle.VoucherType ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
            if (returnedSourceByVoucher.TryGetValue((norm, sle.VoucherId!.Value), out var origId)
                && receiptRateMap.TryGetValue((norm, origId, sle.ItemId, sle.WarehouseId), out var rate))
            {
                result[sle.Id] = rate;
            }
        }

        return result;
    }

    private static ExpectedValuationReplayEngine.LedgerEntryInput BuildReplayEntryInput(
        StockLedgerEntry sle,
        Item item,
        Dictionary<Guid, SerialAndBatchBundle> bundleMap,
        Dictionary<Guid, SerialNo> serialMap,
        Dictionary<Guid, StockReconciliation> reconMap,
        Dictionary<Guid, StockReconciliationItem> reconItemById,
        HashSet<Guid> adjustmentVoucherIds,
        HashSet<Guid> lastAdjustmentSleIds,
        Dictionary<Guid, decimal> purchaseReturnRates,
        Dictionary<Guid, List<ItemStandardCost>> standardCostMap)
    {
        IReadOnlyList<ExpectedValuationReplayEngine.LotMovement>? bundleMovements = null;
        if (sle.SerialAndBatchBundleId.HasValue
            && bundleMap.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundle))
        {
            bundleMovements = bundle.Entries
                .Select(e => new ExpectedValuationReplayEngine.LotMovement(
                    Math.Abs(e.Qty),
                    e.IncomingRate,
                    e.BatchId,
                    e.SerialNo))
                .ToList();
        }

        string? serialNoNumber = null;
        if (sle.SerialNoId.HasValue && serialMap.TryGetValue(sle.SerialNoId.Value, out var sn))
        {
            serialNoNumber = sn.SerialNumber;
        }

        ExpectedValuationReplayEngine.ReconciledBalance? reconciledBalance = null;
        if (IsStockReconciliationVoucher(sle.VoucherType))
        {
            if (sle.VoucherDetailNo.HasValue && reconItemById.TryGetValue(sle.VoucherDetailNo.Value, out var ri))
            {
                reconciledBalance = new ExpectedValuationReplayEngine.ReconciledBalance(
                    ri.NewQuantity, ri.NewValuationRate, ri.CurrentValuationRate);
            }
            else if (sle.VoucherId.HasValue && reconMap.TryGetValue(sle.VoucherId.Value, out var rec))
            {
                var matchedRow = rec.Items.FirstOrDefault(r => r.ItemId == sle.ItemId && r.WarehouseId == sle.WarehouseId);
                if (matchedRow != null)
                {
                    reconciledBalance = new ExpectedValuationReplayEngine.ReconciledBalance(
                        matchedRow.NewQuantity, matchedRow.NewValuationRate, matchedRow.CurrentValuationRate);
                }
            }
        }

        var isAdjustmentVoucher = sle.VoucherId.HasValue
                                  && IsStockReconciliationVoucher(sle.VoucherType)
                                  && adjustmentVoucherIds.Contains(sle.VoucherId.Value);

        IReadOnlyList<ExpectedValuationReplayEngine.AdjustmentRowInput>? adjustmentRows = null;
        if (isAdjustmentVoucher && sle.VoucherId.HasValue && reconMap.TryGetValue(sle.VoucherId.Value, out var adjRec))
        {
            adjustmentRows = adjRec.Items
                .Where(i => i.ItemId == sle.ItemId && i.WarehouseId == sle.WarehouseId)
                .Select(i =>
                {
                    IReadOnlyList<string>? adjSerials = null;
                    Guid? adjBatchId = null;
                    if (i.SerialAndBatchBundleId.HasValue
                        && bundleMap.TryGetValue(i.SerialAndBatchBundleId.Value, out var adjBundle))
                    {
                        var serialList = adjBundle.Entries
                            .Where(e => !string.IsNullOrWhiteSpace(e.SerialNo))
                            .Select(e => e.SerialNo!)
                            .ToList();
                        if (serialList.Count > 0)
                            adjSerials = serialList;
                        adjBatchId = adjBundle.Entries.FirstOrDefault(e => e.BatchId.HasValue)?.BatchId;
                    }
                    return new ExpectedValuationReplayEngine.AdjustmentRowInput(
                        i.NewQuantity, i.NewValuationRate, adjBatchId, adjSerials);
                })
                .ToList();
        }

        decimal? effectiveStandardRate = null;
        if (item.ValuationMethod == ValuationMethod.StandardCost
            && standardCostMap.TryGetValue(item.Id, out var schedules))
        {
            foreach (var sc in schedules)
            {
                if (sc.EffectiveDate.Date > sle.PostingDate.Date)
                    break;
                effectiveStandardRate = sc.StandardRate;
            }
        }

        decimal? purchaseReturnRate = purchaseReturnRates.TryGetValue(sle.Id, out var prRate) ? prRate : null;

        return new ExpectedValuationReplayEngine.LedgerEntryInput(
            sle.Id,
            sle.PostingDate,
            sle.PostingDateTime,
            sle.CreationTime,
            sle.VoucherType,
            sle.VoucherId,
            sle.VoucherDetailNo,
            sle.QuantityChange,
            sle.IncomingRate,
            sle.OutgoingRate,
            sle.BalanceQuantity,
            sle.ValuationRate,
            sle.BalanceValue,
            sle.StockValueDifference,
            sle.BatchId,
            sle.SerialNoId,
            serialNoNumber,
            sle.SerialAndBatchBundleId,
            sle.IsAdjustmentEntry,
            BundleMovements: bundleMovements,
            ReconciledBalance: reconciledBalance,
            IsAdjustmentVoucher: isAdjustmentVoucher,
            IsLastAdjustmentEntry: isAdjustmentVoucher && lastAdjustmentSleIds.Contains(sle.Id),
            AdjustmentRows: adjustmentRows,
            PurchaseReturnRate: purchaseReturnRate,
            EffectiveStandardRate: effectiveStandardRate);
    }

    private async Task<List<Guid>> ExpandDescendantWarehouseIdsAsync(Guid rootWarehouseId)
    {
        var allWarehouses = await _warehouseRepository.GetListAsync();
        var childrenLookup = allWarehouses
            .Where(w => w.ParentWarehouseId.HasValue)
            .ToLookup(w => w.ParentWarehouseId!.Value, w => w.Id);

        var result = new HashSet<Guid> { rootWarehouseId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootWarehouseId);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var childId in childrenLookup[cur])
            {
                if (result.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return result.ToList();
    }

    private async Task<List<Guid>> GetItemIdsForGroupTreeAsync(Guid rootGroupId)
    {
        var allGroups = await _itemGroupRepository.GetListAsync();
        var childrenLookup = allGroups
            .Where(g => g.ParentId.HasValue)
            .ToLookup(g => g.ParentId!.Value, g => g.Id);

        var groupIds = new HashSet<Guid> { rootGroupId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootGroupId);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var childId in childrenLookup[cur])
            {
                if (groupIds.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        var groupNames = allGroups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => g.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matchingItems = await AsyncExecuter.ToListAsync(
            (await _itemRepository.GetQueryableAsync())
            .Where(i => (i.ItemGroupId.HasValue && groupIds.Contains(i.ItemGroupId.Value))
                        || (i.ItemGroup != null && groupNames.Contains(i.ItemGroup)))
            .Select(i => i.Id));

        return matchingItems;
    }

    private static IncorrectSerialNoValuationRowDto CreateBalanceSummaryRow(decimal qty, decimal valuationRate) => new()
    {
        SerialNoNumber = "Balance",
        Qty = qty,
        ValuationRate = valuationRate,
        IsBalanceRow = true,
    };

    private static bool IsIgnoredBundleVoucherType(string? voucherType)
    {
        if (string.IsNullOrWhiteSpace(voucherType)) return false;
        return voucherType.Trim().Replace(" ", string.Empty).ToUpperInvariant() is
            "INSTALLATIONNOTE" or "JOBCARD" or "MAINTENANCESCHEDULE" or "PICKLIST";
    }

    private static bool IsPosInvoiceVoucherType(string? voucherType)
    {
        if (string.IsNullOrWhiteSpace(voucherType)) return false;
        return voucherType.Trim().Replace(" ", string.Empty).ToUpperInvariant() == "POSINVOICE";
    }

    private async Task<DateTime?> FindFirstUnsyncDateAsync(
        Guid companyId,
        Guid accountId,
        List<StockLedgerEntry> scopedSles)
    {
        var minSleDate = scopedSles.Count > 0 ? scopedSles.Min(s => s.PostingDate.Date) : (DateTime?)null;
        var postedJEs = await AsyncExecuter.ToListAsync(
            (await _journalEntryRepository.WithDetailsAsync(j => j.Lines))
            .Where(j => j.CompanyId == companyId && j.Status == DocumentStatus.Posted));

        var glByDate = new Dictionary<DateTime, decimal>();
        foreach (var je in postedJEs)
        {
            var delta = je.Lines
                .Where(l => l.AccountId == accountId)
                .Sum(l => l.IsDebit ? l.Amount : -l.Amount);
            if (delta != 0m)
            {
                var d = je.PostingDate.Date;
                glByDate[d] = glByDate.GetValueOrDefault(d, 0m) + delta;
            }
        }

        var sleByDate = scopedSles
            .GroupBy(s => s.PostingDate.Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.StockValueDifference));

        var candidateDates = glByDate.Keys.Concat(sleByDate.Keys).Distinct().OrderBy(d => d).ToList();
        if (candidateDates.Count == 0)
            return minSleDate;

        decimal runningGl = 0m;
        decimal runningStock = 0m;
        foreach (var d in candidateDates)
        {
            runningGl += glByDate.GetValueOrDefault(d, 0m);
            runningStock += sleByDate.GetValueOrDefault(d, 0m);
            if (Math.Abs(runningGl - runningStock) > 0.1m)
                return d;
        }

        return minSleDate;
    }

    private async Task<Dictionary<Guid, Item>> LoadItemMapAsync(IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, Item>();
        return (await _itemRepository.GetListAsync(i => ids.Contains(i.Id))).ToDictionary(i => i.Id);
    }

    private async Task<Dictionary<Guid, Warehouse>> LoadWarehouseMapAsync(IEnumerable<Guid> warehouseIds)
    {
        var ids = warehouseIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, Warehouse>();
        return (await _warehouseRepository.GetListAsync(w => ids.Contains(w.Id))).ToDictionary(w => w.Id);
    }

    private async Task<Dictionary<(string VoucherType, Guid VoucherId), string>> LoadVoucherNumbersAsync(
        IEnumerable<StockLedgerEntry> sles)
        => await LoadVoucherNumbersByPairsAsync(
            sles.Where(s => !string.IsNullOrWhiteSpace(s.VoucherType) && s.VoucherId.HasValue)
                .Select(s => (s.VoucherType!, s.VoucherId!.Value)));

    private async Task<Dictionary<(string VoucherType, Guid VoucherId), string>> LoadVoucherNumbersByPairsAsync(
        IEnumerable<(string VoucherType, Guid VoucherId)> pairs)
    {
        var result = new Dictionary<(string VoucherType, Guid VoucherId), string>();
        var groups = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.VoucherType) && p.VoucherId != Guid.Empty)
            .GroupBy(p => p.VoucherType)
            .ToDictionary(g => g.Key, g => g.Select(x => x.VoucherId).Distinct().ToList());

        foreach (var (vType, ids) in groups)
        {
            switch (vType.Replace(" ", string.Empty).ToUpperInvariant())
            {
                case "STOCKENTRY":
                    foreach (var x in await _stockEntryRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.EntryNumber ?? x.Id.ToString()[..8];
                    break;
                case "STOCKRECONCILIATION":
                    foreach (var x in await _stockReconciliationRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.ReconciliationNumber ?? x.Id.ToString()[..8];
                    break;
                case "PURCHASERECEIPT":
                    foreach (var x in await _purchaseReceiptRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.ReceiptNumber;
                    break;
                case "DELIVERYNOTE":
                    foreach (var x in await _deliveryNoteRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.DeliveryNumber;
                    break;
                case "SALESINVOICE":
                case "POSINVOICE":
                    foreach (var x in await _salesInvoiceRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.InvoiceNumber;
                    break;
                case "PURCHASEINVOICE":
                    foreach (var x in await _purchaseInvoiceRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.InvoiceNumber;
                    break;
            }
        }

        return result;
    }

    private static string ResolveVoucherNo(
        StockLedgerEntry sle,
        Dictionary<(string VoucherType, Guid VoucherId), string> voucherNoMap)
    {
        if (!string.IsNullOrWhiteSpace(sle.VoucherType) && sle.VoucherId.HasValue
            && voucherNoMap.TryGetValue((sle.VoucherType, sle.VoucherId.Value), out var no))
        {
            return no;
        }

        return sle.VoucherId.HasValue ? sle.VoucherId.Value.ToString()[..8] : sle.Id.ToString()[..8];
    }

    /// <summary>
    /// Compares warehouse Bin stock quantity against active batch ledger quantities
    /// (StockLedgerEntry direct BatchId and non-cancelled SerialAndBatchBundle entries)
    /// for batch-tracked items (ERPNext stock/report/stock_qty_vs_batch_qty).
    /// </summary>
    public async Task<StockQtyVsBatchQtyReportDto> GetStockQtyVsBatchQtyAsync(StockQtyVsBatchQtyRequestDto input)
    {
        var itemQuery = (await _itemRepository.GetQueryableAsync())
            .Where(i => i.HasBatchNo);

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            itemQuery = itemQuery.Where(i => i.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        if (!input.ShowDisabledItems)
            itemQuery = itemQuery.Where(i => i.IsActive);

        var items = await AsyncExecuter.ToListAsync(itemQuery);
        if (items.Count == 0)
            return new StockQtyVsBatchQtyReportDto();

        var itemMap = items.ToDictionary(i => i.Id);
        var itemIds = items.Select(i => i.Id).ToList();

        var whQuery = (await _warehouseRepository.GetQueryableAsync())
            .Where(w => !w.IsGroup);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
            whQuery = whQuery.Where(w => w.Id == input.WarehouseId.Value);

        var warehouses = await AsyncExecuter.ToListAsync(whQuery);
        if (warehouses.Count == 0)
            return new StockQtyVsBatchQtyReportDto();

        var warehouseMap = warehouses.ToDictionary(w => w.Id);
        var warehouseIds = warehouses.Select(w => w.Id).ToList();

        var batchQuery = (await _batchRepository.GetQueryableAsync())
            .Where(b => itemIds.Contains(b.ItemId) && !b.IsCancelled);
        if (!input.ShowDisabledItems)
            batchQuery = batchQuery.Where(b => !b.IsDisabled);
        if (input.BatchId.HasValue && input.BatchId.Value != Guid.Empty)
            batchQuery = batchQuery.Where(b => b.Id == input.BatchId.Value);

        var activeBatches = await AsyncExecuter.ToListAsync(batchQuery);
        var batchMap = activeBatches.ToDictionary(b => b.Id);
        var activeBatchIds = batchMap.Keys.ToHashSet();

        var bins = await AsyncExecuter.ToListAsync(
            (await _binRepository.GetQueryableAsync())
            .Where(b => itemIds.Contains(b.ItemId) && warehouseIds.Contains(b.WarehouseId)));
        var binQtyMap = bins
            .GroupBy(b => (b.ItemId, b.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));

        var sles = await AsyncExecuter.ToListAsync(
            (await _sleRepository.GetQueryableAsync())
            .Where(s => !s.IsCancelled && itemIds.Contains(s.ItemId) && warehouseIds.Contains(s.WarehouseId)));

        var bundleIds = sles
            .Where(s => s.SerialAndBatchBundleId.HasValue)
            .Select(s => s.SerialAndBatchBundleId!.Value)
            .Distinct()
            .ToList();

        var bundleLookup = new Dictionary<Guid, SerialAndBatchBundle>();
        if (bundleIds.Count > 0)
        {
            var bundles = (await _bundleRepository.WithDetailsAsync(b => b.Entries))
                .Where(b => bundleIds.Contains(b.Id) && !b.IsCancelled)
                .ToList();
            bundleLookup = bundles.ToDictionary(b => b.Id);
        }

        // Accumulate net batch quantity per (ItemId, WarehouseId) and per (ItemId, WarehouseId, BatchId)
        var pairBatchTotalMap = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();
        var pairBatchNumbersMap = new Dictionary<(Guid ItemId, Guid WarehouseId), HashSet<string>>();

        void AddBatchMovement(Guid itemId, Guid warehouseId, Guid batchId, decimal qty)
        {
            if (!activeBatchIds.Contains(batchId))
                return;

            var key = (itemId, warehouseId);
            pairBatchTotalMap[key] = pairBatchTotalMap.GetValueOrDefault(key, 0m) + qty;

            if (batchMap.TryGetValue(batchId, out var bMeta))
            {
                if (!pairBatchNumbersMap.TryGetValue(key, out var set))
                    pairBatchNumbersMap[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(bMeta.BatchNo);
            }
        }

        foreach (var sle in sles)
        {
            if (sle.SerialAndBatchBundleId.HasValue &&
                bundleLookup.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundle))
            {
                var sign = bundle.TypeOfTransaction == BundleTransactionType.Outward ? -1m : 1m;
                foreach (var entry in bundle.Entries.Where(e => e.BatchId.HasValue))
                {
                    var whId = entry.WarehouseId ?? sle.WarehouseId;
                    if (!warehouseMap.ContainsKey(whId)) continue;
                    var signedEntryQty = entry.Qty < 0 ? entry.Qty : sign * entry.Qty;
                    AddBatchMovement(sle.ItemId, whId, entry.BatchId!.Value, signedEntryQty);
                }
            }
            else if (sle.BatchId.HasValue)
            {
                AddBatchMovement(sle.ItemId, sle.WarehouseId, sle.BatchId.Value, sle.QuantityChange);
            }
        }

        var allPairs = new HashSet<(Guid ItemId, Guid WarehouseId)>();
        foreach (var k in binQtyMap.Keys) allPairs.Add(k);
        foreach (var k in pairBatchTotalMap.Keys) allPairs.Add(k);

        var rows = new List<StockQtyVsBatchQtyRowDto>();
        var search = input.SearchTerm?.Trim().ToLowerInvariant();

        foreach (var pair in allPairs)
        {
            if (!itemMap.TryGetValue(pair.ItemId, out var item)) continue;
            if (!warehouseMap.TryGetValue(pair.WarehouseId, out var wh)) continue;

            var stockQty = Math.Round(binQtyMap.GetValueOrDefault(pair, 0m), 4);
            var batchQty = Math.Round(pairBatchTotalMap.GetValueOrDefault(pair, 0m), 4);
            var difference = Math.Round(stockQty - batchQty, 4);
            var isMismatch = difference != 0m;

            if (input.ShowOnlyDifferences && !isMismatch)
                continue;

            var batchNos = pairBatchNumbersMap.TryGetValue(pair, out var bSet)
                ? string.Join(", ", bSet.OrderBy(n => n))
                : string.Empty;

            if (!string.IsNullOrEmpty(search))
            {
                var matches = item.ItemCode.ToLowerInvariant().Contains(search)
                    || item.ItemName.ToLowerInvariant().Contains(search)
                    || wh.Name.ToLowerInvariant().Contains(search)
                    || batchNos.ToLowerInvariant().Contains(search);
                if (!matches) continue;
            }

            rows.Add(new StockQtyVsBatchQtyRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                WarehouseId = wh.Id,
                WarehouseName = wh.Name,
                BatchCount = pairBatchNumbersMap.TryGetValue(pair, out var cnt) ? cnt.Count : 0,
                BatchNumbers = batchNos,
                BatchQty = batchQty,
                StockQty = stockQty,
                Difference = difference,
                IsMismatch = isMismatch,
            });
        }

        var sorted = rows
            .OrderByDescending(r => Math.Abs(r.Difference))
            .ThenBy(r => r.ItemCode)
            .ThenBy(r => r.WarehouseName)
            .ToList();

        return new StockQtyVsBatchQtyReportDto
        {
            Rows = sorted,
            TotalMismatches = sorted.Count(r => r.IsMismatch),
            TotalBatchQty = Math.Round(sorted.Sum(r => r.BatchQty), 4),
            TotalStockQty = Math.Round(sorted.Sum(r => r.StockQty), 4),
            TotalDifference = Math.Round(sorted.Sum(r => r.Difference), 4),
        };
    }

    /// <summary>
    /// Reconciles Bin ActualQty from active batch ledger quantities for selected (ItemId, WarehouseId) pairs,
    /// mirroring ERPNext stock_qty_vs_batch_qty.update_batch_qty.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Edit)]
    public async Task<ReconcileBatchStockResultDto> ReconcileBatchStockQtyAsync(ReconcileBatchStockInputDto input)
    {
        if (input.Items == null || input.Items.Count == 0)
            return new ReconcileBatchStockResultDto { UpdatedCount = 0, Message = "No rows selected." };

        var report = await GetStockQtyVsBatchQtyAsync(new StockQtyVsBatchQtyRequestDto
        {
            ShowDisabledItems = true,
            ShowOnlyDifferences = false,
        });

        var rowMap = report.Rows.ToDictionary(r => (r.ItemId, r.WarehouseId));
        var updatedCount = 0;

        foreach (var req in input.Items.Distinct())
        {
            if (!rowMap.TryGetValue((req.ItemId, req.WarehouseId), out var row))
                continue;
            if (row.Difference == 0m)
                continue;

            var bin = (await _binRepository.GetQueryableAsync())
                .FirstOrDefault(b => b.ItemId == req.ItemId && b.WarehouseId == req.WarehouseId);
            if (bin != null)
            {
                bin.ActualQty = row.BatchQty;
                bin.StockValue = Math.Round(bin.ActualQty * bin.ValuationRate, 2);
                await _binRepository.UpdateAsync(bin);
                updatedCount++;
            }
            else if (row.BatchQty != 0m)
            {
                var newBin = new Bin(GuidGenerator.Create(), req.ItemId, req.WarehouseId, CurrentTenant.Id)
                {
                    ActualQty = row.BatchQty,
                };
                await _binRepository.InsertAsync(newBin);
                updatedCount++;
            }
        }

        return new ReconcileBatchStockResultDto
        {
            UpdatedCount = updatedCount,
            Message = $"Reconciled {updatedCount} bin record(s) from batch ledger balances.",
        };
    }
}
