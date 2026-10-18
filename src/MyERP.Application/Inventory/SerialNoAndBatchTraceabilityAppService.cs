using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class SerialNoAndBatchTraceabilityAppService : ApplicationService, ISerialNoAndBatchTraceabilityAppService
{
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;

    public SerialNoAndBatchTraceabilityAppService(
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Customer, Guid> customerRepository)
    {
        _serialNoRepository = serialNoRepository;
        _batchRepository = batchRepository;
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _stockEntryRepository = stockEntryRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _workOrderRepository = workOrderRepository;
        _supplierRepository = supplierRepository;
        _customerRepository = customerRepository;
    }

    public async Task<SerialNoAndBatchTraceabilityReportDto> GetReportAsync(SerialNoAndBatchTraceabilityFilterDto input)
    {
        var targetSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetBatchIds = new HashSet<Guid>();

        // 1. Resolve Targets
        if (!string.IsNullOrWhiteSpace(input.SerialNo))
        {
            targetSerials.Add(input.SerialNo.Trim());
        }

        if (input.BatchId.HasValue && input.BatchId.Value != Guid.Empty)
        {
            targetBatchIds.Add(input.BatchId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(input.BatchNo))
        {
            var bList = await _batchRepository.GetListAsync(b => b.BatchNo == input.BatchNo.Trim());
            foreach (var b in bList) targetBatchIds.Add(b.Id);
        }

        Guid? resolvedItemId = input.ItemId;
        if (!resolvedItemId.HasValue && !string.IsNullOrWhiteSpace(input.ItemCode))
        {
            var item = (await _itemRepository.GetListAsync(i => i.ItemCode == input.ItemCode.Trim())).FirstOrDefault();
            resolvedItemId = item?.Id;
        }

        if (targetSerials.Count == 0 && targetBatchIds.Count == 0 && resolvedItemId.HasValue)
        {
            var serials = await _serialNoRepository.GetListAsync(s => s.ItemId == resolvedItemId.Value);
            foreach (var s in serials) targetSerials.Add(s.SerialNumber);

            var batches = await _batchRepository.GetListAsync(b => b.ItemId == resolvedItemId.Value);
            foreach (var b in batches) targetBatchIds.Add(b.Id);
        }

        if (targetSerials.Count == 0 && targetBatchIds.Count == 0)
        {
            return new SerialNoAndBatchTraceabilityReportDto();
        }

        // Preload active bundles with details
        var bundleQuery = await _bundleRepository.WithDetailsAsync(b => b.Entries);
        bundleQuery = bundleQuery.Where(b => !b.IsCancelled);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            bundleQuery = bundleQuery.Where(b => b.CompanyId == input.CompanyId.Value);
        }
        var allBundles = bundleQuery.ToList();

        // Caches for lookups
        var itemMap = (await _itemRepository.GetListAsync()).ToDictionary(i => i.Id);
        var whMap = (await _warehouseRepository.GetListAsync()).ToDictionary(w => w.Id);
        var batchMap = (await _batchRepository.GetListAsync()).ToDictionary(b => b.Id);
        var serialMap = (await _serialNoRepository.GetListAsync()).ToDictionary(s => s.SerialNumber, StringComparer.OrdinalIgnoreCase);

        var seMap = (await _stockEntryRepository.GetListAsync()).ToDictionary(s => s.Id);
        var prMap = (await _purchaseReceiptRepository.GetListAsync()).ToDictionary(p => p.Id);
        var dnMap = (await _deliveryNoteRepository.GetListAsync()).ToDictionary(d => d.Id);
        var siMap = (await _salesInvoiceRepository.GetListAsync()).ToDictionary(s => s.Id);
        var woMap = (await _workOrderRepository.GetListAsync()).ToDictionary(w => w.Id);
        var supMap = (await _supplierRepository.GetListAsync()).ToDictionary(s => s.Id);
        var custMap = (await _customerRepository.GetListAsync()).ToDictionary(c => c.Id);

        var resultNodes = new List<SerialNoAndBatchTraceabilityNodeDto>();
        var visited = new HashSet<string>();

        var direction = input.TraceabilityDirection?.Trim() ?? "Both";
        bool doBackward = string.Equals(direction, "Backward", StringComparison.OrdinalIgnoreCase) || string.Equals(direction, "Both", StringComparison.OrdinalIgnoreCase);
        bool doForward = string.Equals(direction, "Forward", StringComparison.OrdinalIgnoreCase) || string.Equals(direction, "Both", StringComparison.OrdinalIgnoreCase);

        // --- BACKWARD TRACEABILITY ---
        if (doBackward)
        {
            foreach (var serial in targetSerials)
            {
                await TraceBackwardAsync(
                    serialNo: serial,
                    batchId: null,
                    indent: 0,
                    allBundles: allBundles,
                    resultNodes: resultNodes,
                    visited: visited,
                    itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);
            }

            foreach (var batchId in targetBatchIds)
            {
                await TraceBackwardAsync(
                    serialNo: null,
                    batchId: batchId,
                    indent: 0,
                    allBundles: allBundles,
                    resultNodes: resultNodes,
                    visited: visited,
                    itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);
            }
        }

        // --- FORWARD TRACEABILITY ---
        if (doForward)
        {
            foreach (var serial in targetSerials)
            {
                await TraceForwardAsync(
                    serialNo: serial,
                    batchId: null,
                    indent: 0,
                    allBundles: allBundles,
                    resultNodes: resultNodes,
                    visited: visited,
                    itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);
            }

            foreach (var batchId in targetBatchIds)
            {
                await TraceForwardAsync(
                    serialNo: null,
                    batchId: batchId,
                    indent: 0,
                    allBundles: allBundles,
                    resultNodes: resultNodes,
                    visited: visited,
                    itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);
            }
        }

        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            resultNodes = resultNodes.Where(n =>
                n.ItemCode.ToLowerInvariant().Contains(term) ||
                n.ItemName.ToLowerInvariant().Contains(term) ||
                (n.SerialNo != null && n.SerialNo.ToLowerInvariant().Contains(term)) ||
                (n.BatchNo != null && n.BatchNo.ToLowerInvariant().Contains(term)) ||
                (n.WarehouseName != null && n.WarehouseName.ToLowerInvariant().Contains(term)) ||
                n.ReferenceDocNo.ToLowerInvariant().Contains(term) ||
                (n.SupplierName != null && n.SupplierName.ToLowerInvariant().Contains(term)) ||
                (n.CustomerName != null && n.CustomerName.ToLowerInvariant().Contains(term)) ||
                (n.WorkOrderNo != null && n.WorkOrderNo.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        return new SerialNoAndBatchTraceabilityReportDto
        {
            TotalNodes = resultNodes.Count,
            RootNodesCount = resultNodes.Count(n => n.Indent == 0),
            Nodes = resultNodes
        };
    }

    private Task TraceBackwardAsync(
        string? serialNo,
        Guid? batchId,
        int indent,
        List<SerialAndBatchBundle> allBundles,
        List<SerialNoAndBatchTraceabilityNodeDto> resultNodes,
        HashSet<string> visited,
        Dictionary<Guid, Item> itemMap,
        Dictionary<Guid, Warehouse> whMap,
        Dictionary<Guid, Batch> batchMap,
        Dictionary<string, SerialNo> serialMap,
        Dictionary<Guid, StockEntry> seMap,
        Dictionary<Guid, PurchaseReceipt> prMap,
        Dictionary<Guid, DeliveryNote> dnMap,
        Dictionary<Guid, SalesInvoice> siMap,
        Dictionary<Guid, WorkOrder> woMap,
        Dictionary<Guid, Supplier> supMap,
        Dictionary<Guid, Customer> custMap)
    {
        var inwardBundles = allBundles
            .Where(b => b.TypeOfTransaction == BundleTransactionType.Inward &&
                        b.Entries.Any(e => (serialNo != null && string.Equals(e.SerialNo, serialNo, StringComparison.OrdinalIgnoreCase)) ||
                                           (batchId.HasValue && e.BatchId == batchId.Value)))
            .OrderBy(b => b.PostingDate)
            .ToList();

        foreach (var bundle in inwardBundles)
        {
            var matchingEntry = bundle.Entries.FirstOrDefault(e =>
                (serialNo != null && string.Equals(e.SerialNo, serialNo, StringComparison.OrdinalIgnoreCase)) ||
                (batchId.HasValue && e.BatchId == batchId.Value));

            if (matchingEntry == null) continue;

            var visitKey = $"BACK_{bundle.VoucherType}_{bundle.VoucherId}_{serialNo}_{batchId}";
            if (!visited.Add(visitKey)) continue;

            var node = CreateNode(
                direction: "Backward",
                indent: indent,
                bundle: bundle,
                entry: matchingEntry,
                serialNo: serialNo,
                batchId: batchId,
                itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);

            resultNodes.Add(node);

            // If created via Manufacture/Repack Stock Entry, recursively trace consumed raw materials!
            if (bundle.VoucherType == "StockEntry" && seMap.TryGetValue(bundle.VoucherId, out var se))
            {
                var consumedBundles = allBundles
                    .Where(b => b.VoucherType == "StockEntry" && b.VoucherId == se.Id && b.TypeOfTransaction == BundleTransactionType.Outward)
                    .ToList();

                foreach (var cBundle in consumedBundles)
                {
                    foreach (var cEntry in cBundle.Entries)
                    {
                        // Check if specific FG was mapped
                        bool fgMatches = (serialNo != null && string.Equals(cEntry.FgSerialNo, serialNo, StringComparison.OrdinalIgnoreCase)) ||
                                         (batchId.HasValue && batchMap.TryGetValue(batchId.Value, out var bObj) && string.Equals(cEntry.FgBatchNo, bObj.BatchNo, StringComparison.OrdinalIgnoreCase));

                        // If no specific FG mapping exists in the entry, relate all consumed rows for this StockEntry
                        bool include = fgMatches || (string.IsNullOrEmpty(cEntry.FgSerialNo) && string.IsNullOrEmpty(cEntry.FgBatchNo));

                        if (include && (cEntry.SerialNo != null || cEntry.BatchId.HasValue))
                        {
                            TraceBackwardAsync(
                                cEntry.SerialNo,
                                cEntry.BatchId,
                                indent + 1,
                                allBundles,
                                resultNodes,
                                visited,
                                itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task TraceForwardAsync(
        string? serialNo,
        Guid? batchId,
        int indent,
        List<SerialAndBatchBundle> allBundles,
        List<SerialNoAndBatchTraceabilityNodeDto> resultNodes,
        HashSet<string> visited,
        Dictionary<Guid, Item> itemMap,
        Dictionary<Guid, Warehouse> whMap,
        Dictionary<Guid, Batch> batchMap,
        Dictionary<string, SerialNo> serialMap,
        Dictionary<Guid, StockEntry> seMap,
        Dictionary<Guid, PurchaseReceipt> prMap,
        Dictionary<Guid, DeliveryNote> dnMap,
        Dictionary<Guid, SalesInvoice> siMap,
        Dictionary<Guid, WorkOrder> woMap,
        Dictionary<Guid, Supplier> supMap,
        Dictionary<Guid, Customer> custMap)
    {
        var outwardBundles = allBundles
            .Where(b => b.TypeOfTransaction == BundleTransactionType.Outward &&
                        b.Entries.Any(e => (serialNo != null && string.Equals(e.SerialNo, serialNo, StringComparison.OrdinalIgnoreCase)) ||
                                           (batchId.HasValue && e.BatchId == batchId.Value)))
            .OrderBy(b => b.PostingDate)
            .ToList();

        foreach (var bundle in outwardBundles)
        {
            var matchingEntry = bundle.Entries.FirstOrDefault(e =>
                (serialNo != null && string.Equals(e.SerialNo, serialNo, StringComparison.OrdinalIgnoreCase)) ||
                (batchId.HasValue && e.BatchId == batchId.Value));

            if (matchingEntry == null) continue;

            var visitKey = $"FWD_{bundle.VoucherType}_{bundle.VoucherId}_{serialNo}_{batchId}";
            if (!visited.Add(visitKey)) continue;

            var node = CreateNode(
                direction: "Forward",
                indent: indent,
                bundle: bundle,
                entry: matchingEntry,
                serialNo: serialNo,
                batchId: batchId,
                itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);

            resultNodes.Add(node);

            // If consumed in Stock Entry (Manufacture/Repack), recursively trace produced Finished Goods forward!
            if (bundle.VoucherType == "StockEntry" && seMap.TryGetValue(bundle.VoucherId, out var se))
            {
                var producedBundles = allBundles
                    .Where(b => b.VoucherType == "StockEntry" && b.VoucherId == se.Id && b.TypeOfTransaction == BundleTransactionType.Inward)
                    .ToList();

                foreach (var pBundle in producedBundles)
                {
                    foreach (var pEntry in pBundle.Entries)
                    {
                        bool fgMatches = (matchingEntry.FgSerialNo != null && string.Equals(matchingEntry.FgSerialNo, pEntry.SerialNo, StringComparison.OrdinalIgnoreCase)) ||
                                         (matchingEntry.FgBatchNo != null && pEntry.BatchId.HasValue && batchMap.TryGetValue(pEntry.BatchId.Value, out var bObj) && string.Equals(matchingEntry.FgBatchNo, bObj.BatchNo, StringComparison.OrdinalIgnoreCase));

                        bool include = fgMatches || (string.IsNullOrEmpty(matchingEntry.FgSerialNo) && string.IsNullOrEmpty(matchingEntry.FgBatchNo));

                        if (include && (pEntry.SerialNo != null || pEntry.BatchId.HasValue))
                        {
                            TraceForwardAsync(
                                pEntry.SerialNo,
                                pEntry.BatchId,
                                indent + 1,
                                allBundles,
                                resultNodes,
                                visited,
                                itemMap, whMap, batchMap, serialMap, seMap, prMap, dnMap, siMap, woMap, supMap, custMap);
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private SerialNoAndBatchTraceabilityNodeDto CreateNode(
        string direction,
        int indent,
        SerialAndBatchBundle bundle,
        SerialAndBatchEntry entry,
        string? serialNo,
        Guid? batchId,
        Dictionary<Guid, Item> itemMap,
        Dictionary<Guid, Warehouse> whMap,
        Dictionary<Guid, Batch> batchMap,
        Dictionary<string, SerialNo> serialMap,
        Dictionary<Guid, StockEntry> seMap,
        Dictionary<Guid, PurchaseReceipt> prMap,
        Dictionary<Guid, DeliveryNote> dnMap,
        Dictionary<Guid, SalesInvoice> siMap,
        Dictionary<Guid, WorkOrder> woMap,
        Dictionary<Guid, Supplier> supMap,
        Dictionary<Guid, Customer> custMap)
    {
        itemMap.TryGetValue(bundle.ItemId, out var item);
        var whId = entry.WarehouseId ?? bundle.WarehouseId;
        whMap.TryGetValue(whId, out var wh);

        string? batchNo = null;
        DateTime? batchExpiryDate = null;
        if (entry.BatchId.HasValue && batchMap.TryGetValue(entry.BatchId.Value, out var bObj))
        {
            batchNo = bObj.BatchNo;
            batchExpiryDate = bObj.ExpiryDate;
        }

        DateTime? warrantyExpiryDate = null;
        DateTime? amcExpiryDate = null;
        if (entry.SerialNo != null && serialMap.TryGetValue(entry.SerialNo, out var sObj))
        {
            warrantyExpiryDate = sObj.WarrantyExpiryDate;
            amcExpiryDate = sObj.AmcExpiryDate;
        }

        string docNo = bundle.VoucherId.ToString()[..8];
        Guid? supplierId = null;
        string? supplierName = null;
        Guid? customerId = null;
        string? customerName = null;
        Guid? workOrderId = null;
        string? workOrderNo = null;

        switch (bundle.VoucherType)
        {
            case "StockEntry":
                if (seMap.TryGetValue(bundle.VoucherId, out var se))
                {
                    docNo = se.EntryNumber ?? docNo;
                    if (se.WorkOrderId.HasValue && woMap.TryGetValue(se.WorkOrderId.Value, out var wo))
                    {
                        workOrderId = wo.Id;
                        workOrderNo = wo.WorkOrderNumber;
                    }
                }
                break;

            case "PurchaseReceipt":
                if (prMap.TryGetValue(bundle.VoucherId, out var pr))
                {
                    docNo = pr.ReceiptNumber ?? docNo;
                    supplierId = pr.SupplierId;
                    if (supMap.TryGetValue(pr.SupplierId, out var sup))
                        supplierName = sup.Name;
                }
                break;

            case "DeliveryNote":
                if (dnMap.TryGetValue(bundle.VoucherId, out var dn))
                {
                    docNo = dn.DeliveryNumber ?? docNo;
                    customerId = dn.CustomerId;
                    if (custMap.TryGetValue(dn.CustomerId, out var cust))
                        customerName = cust.Name;
                }
                break;

            case "SalesInvoice":
                if (siMap.TryGetValue(bundle.VoucherId, out var si))
                {
                    docNo = si.InvoiceNumber ?? docNo;
                    customerId = si.CustomerId;
                    if (custMap.TryGetValue(si.CustomerId, out var cust))
                        customerName = cust.Name;
                }
                break;
        }

        return new SerialNoAndBatchTraceabilityNodeDto
        {
            NodeId = Guid.NewGuid(),
            Direction = direction,
            Indent = indent,
            ItemId = bundle.ItemId,
            ItemCode = item?.ItemCode ?? string.Empty,
            ItemName = item?.ItemName ?? string.Empty,
            SerialNo = entry.SerialNo ?? serialNo,
            BatchId = entry.BatchId ?? batchId,
            BatchNo = batchNo,
            WarehouseId = whId,
            WarehouseName = wh?.Name ?? string.Empty,
            Quantity = entry.Qty,
            ReferenceDocType = bundle.VoucherType,
            ReferenceDocId = bundle.VoucherId,
            ReferenceDocNo = docNo,
            PostingDateTime = bundle.PostingDate,
            SupplierId = supplierId,
            SupplierName = supplierName,
            CustomerId = customerId,
            CustomerName = customerName,
            WorkOrderId = workOrderId,
            WorkOrderNo = workOrderNo,
            BatchExpiryDate = batchExpiryDate,
            WarrantyExpiryDate = warrantyExpiryDate,
            AmcExpiryDate = amcExpiryDate
        };
    }
}
