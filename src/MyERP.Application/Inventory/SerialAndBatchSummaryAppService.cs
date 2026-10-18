using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class SerialAndBatchSummaryAppService : ApplicationService, ISerialAndBatchSummaryAppService
{
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<StockReconciliation, Guid> _stockReconciliationRepository;

    public SerialAndBatchSummaryAppService(
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<StockReconciliation, Guid> stockReconciliationRepository)
    {
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _batchRepository = batchRepository;
        _stockEntryRepository = stockEntryRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockReconciliationRepository = stockReconciliationRepository;
    }

    public async Task<SerialAndBatchSummaryReportDto> GetReportAsync(SerialAndBatchSummaryFilterDto input)
    {
        var query = await _bundleRepository.WithDetailsAsync(b => b.Entries);

        if (!input.IncludeCancelled)
            query = query.Where(b => !b.IsCancelled);

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);

        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
            query = query.Where(b => b.ItemId == input.ItemId.Value);

        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
            query = query.Where(b => b.WarehouseId == input.WarehouseId.Value);

        if (!string.IsNullOrWhiteSpace(input.VoucherType) && !string.Equals(input.VoucherType, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(b => b.VoucherType == input.VoucherType.Trim());

        if (input.FromDate.HasValue)
            query = query.Where(b => b.PostingDate >= input.FromDate.Value.Date);

        if (input.ToDate.HasValue)
            query = query.Where(b => b.PostingDate <= input.ToDate.Value.Date);

        var bundles = query.OrderByDescending(b => b.PostingDate).ToList();
        if (bundles.Count == 0)
        {
            return new SerialAndBatchSummaryReportDto();
        }

        var itemIds = bundles.Select(b => b.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id))).ToDictionary(i => i.Id);

        var whIds = bundles.Select(b => b.WarehouseId).Distinct().ToList();
        var whMap = (await _warehouseRepository.GetListAsync(w => whIds.Contains(w.Id))).ToDictionary(w => w.Id);

        var batchIds = bundles.SelectMany(b => b.Entries).Where(e => e.BatchId.HasValue).Select(e => e.BatchId!.Value).Distinct().ToList();
        var batchMap = batchIds.Count > 0
            ? (await _batchRepository.GetListAsync(b => batchIds.Contains(b.Id))).ToDictionary(b => b.Id)
            : new Dictionary<Guid, Batch>();

        var voucherMap = await ResolveVoucherNumbersAsync(bundles);

        var rows = new List<SerialAndBatchSummaryRowDto>();

        foreach (var b in bundles)
        {
            itemMap.TryGetValue(b.ItemId, out var item);
            whMap.TryGetValue(b.WarehouseId, out var wh);
            var voucherNo = voucherMap.GetValueOrDefault((b.VoucherType, b.VoucherId)) ?? b.VoucherId.ToString()[..8];

            if (b.Entries.Count == 0)
            {
                rows.Add(new SerialAndBatchSummaryRowDto
                {
                    BundleId = b.Id,
                    VoucherType = b.VoucherType,
                    VoucherId = b.VoucherId,
                    VoucherNo = voucherNo,
                    PostingDate = b.PostingDate,
                    ItemId = b.ItemId,
                    ItemCode = item?.ItemCode ?? string.Empty,
                    ItemName = item?.ItemName ?? string.Empty,
                    WarehouseId = b.WarehouseId,
                    WarehouseName = wh?.Name ?? string.Empty,
                    TypeOfTransaction = b.TypeOfTransaction.ToString(),
                    Quantity = b.TotalQty,
                    IncomingRate = b.AvgRate,
                    StockValueDifference = b.TotalAmount,
                    IsCancelled = b.IsCancelled
                });
            }
            else
            {
                foreach (var entry in b.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(input.SerialNo) && !string.Equals(entry.SerialNo, input.SerialNo.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (input.BatchId.HasValue && entry.BatchId != input.BatchId.Value)
                        continue;

                    string? batchNo = null;
                    if (entry.BatchId.HasValue && batchMap.TryGetValue(entry.BatchId.Value, out var batch))
                    {
                        batchNo = batch.BatchNo;
                    }

                    var rowWhId = entry.WarehouseId ?? b.WarehouseId;
                    whMap.TryGetValue(rowWhId, out var entryWh);

                    rows.Add(new SerialAndBatchSummaryRowDto
                    {
                        BundleId = b.Id,
                        VoucherType = b.VoucherType,
                        VoucherId = b.VoucherId,
                        VoucherNo = voucherNo,
                        PostingDate = b.PostingDate,
                        ItemId = b.ItemId,
                        ItemCode = item?.ItemCode ?? string.Empty,
                        ItemName = item?.ItemName ?? string.Empty,
                        WarehouseId = rowWhId,
                        WarehouseName = entryWh?.Name ?? wh?.Name ?? string.Empty,
                        TypeOfTransaction = b.TypeOfTransaction.ToString(),
                        SerialNo = entry.SerialNo,
                        BatchId = entry.BatchId,
                        BatchNo = batchNo,
                        Quantity = entry.Qty,
                        IncomingRate = entry.IncomingRate,
                        StockValueDifference = entry.StockValueDifference,
                        IsCancelled = b.IsCancelled
                    });
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(input.VoucherNo))
        {
            var vNo = input.VoucherNo.Trim().ToLowerInvariant();
            rows = rows.Where(r => r.VoucherNo.ToLowerInvariant().Contains(vNo)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.VoucherNo.ToLowerInvariant().Contains(term) ||
                r.VoucherType.ToLowerInvariant().Contains(term) ||
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                (r.SerialNo != null && r.SerialNo.ToLowerInvariant().Contains(term)) ||
                (r.BatchNo != null && r.BatchNo.ToLowerInvariant().Contains(term)) ||
                r.WarehouseName.ToLowerInvariant().Contains(term)
            ).ToList();
        }

        var inwardRows = rows.Where(r => r.TypeOfTransaction == "Inward").ToList();
        var outwardRows = rows.Where(r => r.TypeOfTransaction == "Outward").ToList();

        return new SerialAndBatchSummaryReportDto
        {
            TotalBundles = rows.Select(r => r.BundleId).Distinct().Count(),
            TotalEntries = rows.Count,
            TotalInwardQty = inwardRows.Sum(r => Math.Abs(r.Quantity)),
            TotalOutwardQty = outwardRows.Sum(r => Math.Abs(r.Quantity)),
            TotalStockValue = rows.Sum(r => r.StockValueDifference),
            Rows = rows
        };
    }

    private async Task<Dictionary<(string VoucherType, Guid VoucherId), string>> ResolveVoucherNumbersAsync(List<SerialAndBatchBundle> bundles)
    {
        var map = new Dictionary<(string VoucherType, Guid VoucherId), string>();
        var groups = bundles.GroupBy(b => b.VoucherType).ToDictionary(g => g.Key, g => g.Select(b => b.VoucherId).Distinct().ToList());

        foreach (var (vType, ids) in groups)
        {
            switch (vType)
            {
                case "StockEntry":
                    var seList = await _stockEntryRepository.GetListAsync(se => ids.Contains(se.Id));
                    foreach (var se in seList)
                        map[(vType, se.Id)] = se.EntryNumber ?? se.Id.ToString()[..8];
                    break;

                case "PurchaseReceipt":
                    var prList = await _purchaseReceiptRepository.GetListAsync(pr => ids.Contains(pr.Id));
                    foreach (var pr in prList)
                        map[(vType, pr.Id)] = pr.ReceiptNumber ?? pr.Id.ToString()[..8];
                    break;

                case "DeliveryNote":
                    var dnList = await _deliveryNoteRepository.GetListAsync(dn => ids.Contains(dn.Id));
                    foreach (var dn in dnList)
                        map[(vType, dn.Id)] = dn.DeliveryNumber ?? dn.Id.ToString()[..8];
                    break;

                case "SalesInvoice":
                    var siList = await _salesInvoiceRepository.GetListAsync(si => ids.Contains(si.Id));
                    foreach (var si in siList)
                        map[(vType, si.Id)] = si.InvoiceNumber ?? si.Id.ToString()[..8];
                    break;

                case "PurchaseInvoice":
                    var piList = await _purchaseInvoiceRepository.GetListAsync(pi => ids.Contains(pi.Id));
                    foreach (var pi in piList)
                        map[(vType, pi.Id)] = pi.InvoiceNumber ?? pi.Id.ToString()[..8];
                    break;

                case "StockReconciliation":
                    var srList = await _stockReconciliationRepository.GetListAsync(sr => ids.Contains(sr.Id));
                    foreach (var sr in srList)
                        map[(vType, sr.Id)] = sr.ReconciliationNumber ?? sr.Id.ToString()[..8];
                    break;
            }
        }

        return map;
    }
}
