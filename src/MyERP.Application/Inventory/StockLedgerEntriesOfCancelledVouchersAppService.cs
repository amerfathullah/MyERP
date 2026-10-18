using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockLedgerEntriesOfCancelledVouchersAppService : ApplicationService, IStockLedgerEntriesOfCancelledVouchersAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<StockReconciliation, Guid> _stockReconciliationRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepostItemValuationAppService _repostService;

    public StockLedgerEntriesOfCancelledVouchersAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<StockReconciliation, Guid> stockReconciliationRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepostItemValuationAppService repostService)
    {
        _sleRepository = sleRepository;
        _stockEntryRepository = stockEntryRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockReconciliationRepository = stockReconciliationRepository;
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _repostService = repostService;
    }

    public async Task<StockLedgerEntriesOfCancelledVouchersReportDto> GetReportAsync(StockLedgerEntriesOfCancelledVouchersFilterDto input)
    {
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && s.VoucherId.HasValue && !string.IsNullOrEmpty(s.VoucherType));

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);

        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);

        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);

        if (!string.IsNullOrWhiteSpace(input.VoucherType) && !string.Equals(input.VoucherType, "All", StringComparison.OrdinalIgnoreCase))
            sleQuery = sleQuery.Where(s => s.VoucherType == input.VoucherType.Trim());

        if (input.FromDate.HasValue)
            sleQuery = sleQuery.Where(s => s.PostingDate >= input.FromDate.Value.Date);

        if (input.ToDate.HasValue)
            sleQuery = sleQuery.Where(s => s.PostingDate <= input.ToDate.Value.Date);

        var sles = sleQuery.ToList();
        if (sles.Count == 0)
        {
            return new StockLedgerEntriesOfCancelledVouchersReportDto();
        }

        // Group active SLEs by VoucherType to batch query their status
        var cancelledVouchers = new Dictionary<(string VoucherType, Guid VoucherId), string>();
        var vGroups = sles.GroupBy(s => s.VoucherType!).ToDictionary(g => g.Key, g => g.Select(s => s.VoucherId!.Value).Distinct().ToList());

        foreach (var (vType, ids) in vGroups)
        {
            switch (vType)
            {
                case "StockEntry":
                    var seList = await _stockEntryRepository.GetListAsync(se => ids.Contains(se.Id) && se.Status == DocumentStatus.Cancelled);
                    foreach (var se in seList)
                        cancelledVouchers[(vType, se.Id)] = se.EntryNumber ?? se.Id.ToString()[..8];
                    break;

                case "PurchaseReceipt":
                    var prList = await _purchaseReceiptRepository.GetListAsync(pr => ids.Contains(pr.Id) && pr.Status == DocumentStatus.Cancelled);
                    foreach (var pr in prList)
                        cancelledVouchers[(vType, pr.Id)] = pr.ReceiptNumber ?? pr.Id.ToString()[..8];
                    break;

                case "DeliveryNote":
                    var dnList = await _deliveryNoteRepository.GetListAsync(dn => ids.Contains(dn.Id) && dn.Status == DocumentStatus.Cancelled);
                    foreach (var dn in dnList)
                        cancelledVouchers[(vType, dn.Id)] = dn.DeliveryNumber ?? dn.Id.ToString()[..8];
                    break;

                case "SalesInvoice":
                    var siList = await _salesInvoiceRepository.GetListAsync(si => ids.Contains(si.Id) && si.Status == DocumentStatus.Cancelled);
                    foreach (var si in siList)
                        cancelledVouchers[(vType, si.Id)] = si.InvoiceNumber ?? si.Id.ToString()[..8];
                    break;

                case "PurchaseInvoice":
                    var piList = await _purchaseInvoiceRepository.GetListAsync(pi => ids.Contains(pi.Id) && pi.Status == DocumentStatus.Cancelled);
                    foreach (var pi in piList)
                        cancelledVouchers[(vType, pi.Id)] = pi.InvoiceNumber ?? pi.Id.ToString()[..8];
                    break;

                case "StockReconciliation":
                    var srList = await _stockReconciliationRepository.GetListAsync(sr => ids.Contains(sr.Id) && sr.Status == DocumentStatus.Cancelled);
                    foreach (var sr in srList)
                        cancelledVouchers[(vType, sr.Id)] = sr.ReconciliationNumber ?? sr.Id.ToString()[..8];
                    break;
            }
        }

        var orphanedSles = sles
            .Where(s => cancelledVouchers.ContainsKey((s.VoucherType!, s.VoucherId!.Value)))
            .ToList();

        if (orphanedSles.Count == 0)
        {
            return new StockLedgerEntriesOfCancelledVouchersReportDto();
        }

        var itemIds = orphanedSles.Select(s => s.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id))).ToDictionary(i => i.Id);

        var whIds = orphanedSles.Select(s => s.WarehouseId).Distinct().ToList();
        var whMap = (await _warehouseRepository.GetListAsync(w => whIds.Contains(w.Id))).ToDictionary(w => w.Id);

        var bundleIds = orphanedSles.Where(s => s.SerialAndBatchBundleId.HasValue).Select(s => s.SerialAndBatchBundleId!.Value).Distinct().ToList();
        var bundleMap = bundleIds.Count > 0
            ? (await _bundleRepository.GetListAsync(b => bundleIds.Contains(b.Id))).ToDictionary(b => b.Id)
            : new Dictionary<Guid, SerialAndBatchBundle>();

        var rows = new List<StockLedgerEntryOfCancelledVoucherRowDto>();
        foreach (var sle in orphanedSles)
        {
            var voucherNo = cancelledVouchers.GetValueOrDefault((sle.VoucherType!, sle.VoucherId!.Value)) ?? sle.VoucherId!.Value.ToString()[..8];
            itemMap.TryGetValue(sle.ItemId, out var item);
            whMap.TryGetValue(sle.WarehouseId, out var wh);

            bool isBundleActive = false;
            string? bundleName = null;
            if (sle.SerialAndBatchBundleId.HasValue && bundleMap.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundle))
            {
                isBundleActive = !bundle.IsCancelled;
                bundleName = bundle.Id.ToString()[..8];
            }

            if (!string.IsNullOrWhiteSpace(input.SearchTerm))
            {
                var term = input.SearchTerm.Trim().ToLowerInvariant();
                var itemMatch = (item?.ItemCode?.ToLowerInvariant().Contains(term) ?? false) || (item?.ItemName?.ToLowerInvariant().Contains(term) ?? false);
                var whMatch = wh?.Name?.ToLowerInvariant().Contains(term) ?? false;
                var voucherMatch = voucherNo.ToLowerInvariant().Contains(term) || sle.VoucherType!.ToLowerInvariant().Contains(term);
                if (!itemMatch && !whMatch && !voucherMatch)
                    continue;
            }

            rows.Add(new StockLedgerEntryOfCancelledVoucherRowDto
            {
                SleId = sle.Id,
                VoucherType = sle.VoucherType!,
                VoucherId = sle.VoucherId!.Value,
                VoucherNo = voucherNo,
                PostingDate = sle.PostingDate,
                PostingTime = sle.PostingTime,
                PostingDateTime = sle.PostingDateTime,
                ItemId = sle.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                WarehouseId = sle.WarehouseId,
                WarehouseName = wh?.Name ?? string.Empty,
                QuantityChange = sle.QuantityChange,
                StockValueDifference = sle.StockValueDifference,
                SerialAndBatchBundleId = sle.SerialAndBatchBundleId,
                SerialAndBatchBundleName = bundleName,
                IsBundleActive = isBundleActive
            });
        }

        rows = rows.OrderBy(r => r.PostingDateTime).ThenBy(r => r.VoucherNo).ToList();

        return new StockLedgerEntriesOfCancelledVouchersReportDto
        {
            TotalUncancelledEntries = rows.Count,
            DistinctCancelledVouchersCount = rows.Select(r => (r.VoucherType, r.VoucherId)).Distinct().Count(),
            TotalDiscrepancyQty = rows.Sum(r => r.QuantityChange),
            TotalDiscrepancyValue = rows.Sum(r => r.StockValueDifference),
            Rows = rows
        };
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<FixUncancelledEntriesResultDto> FixUncancelledEntriesAsync(FixUncancelledEntriesInputDto input)
    {
        if (input.VoucherKeys == null || input.VoucherKeys.Count == 0)
        {
            return new FixUncancelledEntriesResultDto
            {
                Messages = new List<string> { "No vouchers selected to repair." }
            };
        }

        int totalSlesCancelled = 0;
        int totalBundlesCancelled = 0;
        int repostCreatedCount = 0;
        var messages = new List<string>();

        var distinctVouchers = input.VoucherKeys
            .GroupBy(k => new { k.VoucherType, k.VoucherId })
            .Select(g => g.First())
            .ToList();

        foreach (var v in distinctVouchers)
        {
            bool isActuallyCancelled = await IsVoucherCancelledAsync(v.VoucherType, v.VoucherId);
            if (!isActuallyCancelled)
            {
                messages.Add($"Skipped {v.VoucherType} ({v.VoucherId}): document is not cancelled.");
                continue;
            }

            var activeSles = await _sleRepository.GetListAsync(s => s.VoucherType == v.VoucherType && s.VoucherId == v.VoucherId && !s.IsCancelled);
            if (activeSles.Count == 0)
            {
                continue;
            }

            foreach (var sle in activeSles)
            {
                sle.IsCancelled = true;
                await _sleRepository.UpdateAsync(sle);
                totalSlesCancelled++;
            }

            var activeBundles = await _bundleRepository.GetListAsync(b => b.VoucherType == v.VoucherType && b.VoucherId == v.VoucherId && !b.IsCancelled);
            foreach (var bundle in activeBundles)
            {
                bundle.Cancel();
                await _bundleRepository.UpdateAsync(bundle);
                totalBundlesCancelled++;
            }

            var itemWarehouses = activeSles
                .GroupBy(s => new { s.ItemId, s.WarehouseId, s.CompanyId })
                .ToList();

            foreach (var group in itemWarehouses)
            {
                var earliestDate = group.Min(s => s.PostingDate);

                await _repostService.CreateAsync(new CreateRepostItemValuationDto
                {
                    CompanyId = group.Key.CompanyId,
                    BasedOn = 0, // ItemAndWarehouse
                    ItemId = group.Key.ItemId,
                    WarehouseId = group.Key.WarehouseId,
                    PostingDate = earliestDate,
                    RepostGlEntries = true,
                    VoucherType = v.VoucherType,
                    VoucherId = v.VoucherId
                });

                repostCreatedCount++;
            }

            messages.Add($"Repaired {v.VoucherType}: cancelled {activeSles.Count} SLE(s), {activeBundles.Count} bundle(s), queued {itemWarehouses.Count} repost(s).");
        }

        return new FixUncancelledEntriesResultDto
        {
            CancelledSleCount = totalSlesCancelled,
            CancelledBundleCount = totalBundlesCancelled,
            RepostItemValuationCreatedCount = repostCreatedCount,
            Messages = messages
        };
    }

    private async Task<bool> IsVoucherCancelledAsync(string voucherType, Guid voucherId)
    {
        switch (voucherType)
        {
            case "StockEntry":
                var se = await _stockEntryRepository.FindAsync(voucherId);
                return se?.Status == DocumentStatus.Cancelled;

            case "PurchaseReceipt":
                var pr = await _purchaseReceiptRepository.FindAsync(voucherId);
                return pr?.Status == DocumentStatus.Cancelled;

            case "DeliveryNote":
                var dn = await _deliveryNoteRepository.FindAsync(voucherId);
                return dn?.Status == DocumentStatus.Cancelled;

            case "SalesInvoice":
                var si = await _salesInvoiceRepository.FindAsync(voucherId);
                return si?.Status == DocumentStatus.Cancelled;

            case "PurchaseInvoice":
                var pi = await _purchaseInvoiceRepository.FindAsync(voucherId);
                return pi?.Status == DocumentStatus.Cancelled;

            case "StockReconciliation":
                var sr = await _stockReconciliationRepository.FindAsync(voucherId);
                return sr?.Status == DocumentStatus.Cancelled;

            default:
                return false;
        }
    }
}
