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
        IRepository<JournalEntry, Guid> journalEntryRepository)
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
    {
        var result = new Dictionary<(string VoucherType, Guid VoucherId), string>();
        var groups = sles
            .Where(s => !string.IsNullOrWhiteSpace(s.VoucherType) && s.VoucherId.HasValue)
            .GroupBy(s => s.VoucherType!)
            .ToDictionary(g => g.Key, g => g.Select(s => s.VoucherId!.Value).Distinct().ToList());

        foreach (var (vType, ids) in groups)
        {
            switch (vType)
            {
                case "StockEntry":
                    foreach (var x in await _stockEntryRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.EntryNumber ?? x.Id.ToString()[..8];
                    break;
                case "StockReconciliation":
                    foreach (var x in await _stockReconciliationRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.ReconciliationNumber ?? x.Id.ToString()[..8];
                    break;
                case "PurchaseReceipt":
                    foreach (var x in await _purchaseReceiptRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.ReceiptNumber;
                    break;
                case "DeliveryNote":
                    foreach (var x in await _deliveryNoteRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.DeliveryNumber;
                    break;
                case "SalesInvoice":
                    foreach (var x in await _salesInvoiceRepository.GetListAsync(e => ids.Contains(e.Id)))
                        result[(vType, x.Id)] = x.InvoiceNumber;
                    break;
                case "PurchaseInvoice":
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
