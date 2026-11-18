using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

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
        IRepository<SerialNo, Guid> serialNoRepository)
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
}
