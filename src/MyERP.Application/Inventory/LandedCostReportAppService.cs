using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Landed Cost Report — analyzes landed cost vouchers and how additional costs are distributed across receipt documents.
/// Maps to ERPNext stock/report/landed_cost_report/landed_cost_report.py.
/// </summary>
[Authorize(MyERPPermissions.LandedCostVouchers.Default)]
public class LandedCostReportAppService : ApplicationService, ILandedCostReportAppService
{
    private readonly IRepository<LandedCostVoucher, Guid> _lcvRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public LandedCostReportAppService(
        IRepository<LandedCostVoucher, Guid> lcvRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _lcvRepository = lcvRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockEntryRepository = stockEntryRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
    }

    public async Task<LandedCostReportDto> GetReportAsync(LandedCostReportFilterDto input)
    {
        var lcvQuery = await _lcvRepository.WithDetailsAsync();
        var lcvList = lcvQuery.ToList();

        // Filter by Company
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            lcvList = lcvList.Where(l => l.CompanyId == input.CompanyId.Value).ToList();
        }

        // Filter by Status (defaults to Submitted if not specified, but if Status provided, filter by it)
        if (input.Status.HasValue)
        {
            lcvList = lcvList.Where(l => l.Status == input.Status.Value).ToList();
        }

        // Filter by Date
        if (input.FromDate.HasValue)
        {
            var from = input.FromDate.Value.Date;
            lcvList = lcvList.Where(l => l.PostingDate.Date >= from).ToList();
        }
        if (input.ToDate.HasValue)
        {
            var to = input.ToDate.Value.Date;
            lcvList = lcvList.Where(l => l.PostingDate.Date <= to).ToList();
        }

        // Preload metadata
        var suppliers = await _supplierRepository.GetListAsync();
        var suppliersById = suppliers.ToDictionary(s => s.Id, s => s.Name);

        var items = await _itemRepository.GetListAsync();
        var itemsById = items.ToDictionary(i => i.Id, i => i.ItemCode);

        // Preload receipts
        var prList = await _purchaseReceiptRepository.GetListAsync();
        var prById = prList.ToDictionary(p => p.Id, p => p);

        var piList = await _purchaseInvoiceRepository.GetListAsync();
        var piById = piList.ToDictionary(p => p.Id, p => p);

        var seList = await _stockEntryRepository.GetListAsync();
        var seById = seList.ToDictionary(s => s.Id, s => s);

        var reportRows = new List<LandedCostReportRowDto>();

        foreach (var lcv in lcvList)
        {
            var chargesSummary = string.Join("; ", lcv.Charges.Select(c => $"{c.Description}: {c.Amount:N2}"));

            // Group items by receipt
            var receiptGroups = lcv.Items.GroupBy(i => new { i.ReceiptType, i.ReceiptId });

            foreach (var group in receiptGroups)
            {
                var receiptType = group.Key.ReceiptType;
                var receiptId = group.Key.ReceiptId;

                // Filter by ReceiptDocumentType
                if (!string.IsNullOrWhiteSpace(input.ReceiptDocumentType) &&
                    !string.Equals(input.ReceiptDocumentType, "All", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(input.ReceiptDocumentType, receiptType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string receiptNo = string.Empty;
                Guid? supplierId = null;
                string? supplierName = null;
                decimal receiptTotalAmount = 0m;

                if (string.Equals(receiptType, "PurchaseReceipt", StringComparison.OrdinalIgnoreCase) &&
                    prById.TryGetValue(receiptId, out var pr))
                {
                    receiptNo = pr.ReceiptNumber;
                    supplierId = pr.SupplierId;
                    suppliersById.TryGetValue(pr.SupplierId, out supplierName);
                    receiptTotalAmount = pr.GrandTotal;
                }
                else if (string.Equals(receiptType, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase) &&
                         piById.TryGetValue(receiptId, out var pi))
                {
                    receiptNo = pi.InvoiceNumber;
                    supplierId = pi.SupplierId;
                    suppliersById.TryGetValue(pi.SupplierId, out supplierName);
                    receiptTotalAmount = pi.GrandTotal;
                }
                else if (string.Equals(receiptType, "StockEntry", StringComparison.OrdinalIgnoreCase) &&
                         seById.TryGetValue(receiptId, out var se))
                {
                    receiptNo = se.EntryNumber ?? string.Empty;
                    receiptTotalAmount = se.TotalIncomingValue;
                }

                // Filter by ReceiptDocumentNo
                if (!string.IsNullOrWhiteSpace(input.ReceiptDocumentNo) &&
                    (string.IsNullOrEmpty(receiptNo) || !receiptNo.Contains(input.ReceiptDocumentNo.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var itemsSummary = string.Join(", ", group.Select(gi =>
                    itemsById.TryGetValue(gi.ItemId, out var ic) ? ic : gi.ItemId.ToString()[..8]));

                var allocatedCharges = group.Sum(gi => gi.ApplicableCharges);

                reportRows.Add(new LandedCostReportRowDto
                {
                    LandedCostVoucherId = lcv.Id,
                    LandedCostVoucherNo = lcv.VoucherNumber ?? string.Empty,
                    PostingDate = lcv.PostingDate,
                    Status = lcv.Status,
                    DistributionMethod = lcv.DistributionMethod,
                    TotalLandedCost = lcv.TotalCharges,
                    ReceiptDocumentType = receiptType,
                    ReceiptDocumentId = receiptId,
                    ReceiptDocumentNo = receiptNo,
                    SupplierId = supplierId,
                    SupplierName = supplierName,
                    ReceiptTotalAmount = receiptTotalAmount,
                    AllocatedCharges = allocatedCharges,
                    ItemCount = group.Count(),
                    ChargesSummary = chargesSummary,
                    ItemsSummary = itemsSummary
                });
            }
        }

        // Filter by SearchTerm if provided
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            reportRows = reportRows.Where(r =>
                r.LandedCostVoucherNo.ToLowerInvariant().Contains(term) ||
                r.ReceiptDocumentNo.ToLowerInvariant().Contains(term) ||
                (r.SupplierName != null && r.SupplierName.ToLowerInvariant().Contains(term)) ||
                r.ChargesSummary.ToLowerInvariant().Contains(term) ||
                r.ItemsSummary.ToLowerInvariant().Contains(term)
            ).ToList();
        }

        reportRows = reportRows.OrderByDescending(r => r.PostingDate).ThenBy(r => r.LandedCostVoucherNo).ToList();

        var distinctVoucherIds = reportRows.Select(r => r.LandedCostVoucherId).Distinct().ToHashSet();
        var totalCost = lcvList.Where(l => distinctVoucherIds.Contains(l.Id)).Sum(l => l.TotalCharges);
        var distinctReceipts = reportRows.Select(r => r.ReceiptDocumentId).Distinct().Count();

        return new LandedCostReportDto
        {
            TotalVouchersCount = distinctVoucherIds.Count,
            TotalLandedCost = totalCost,
            TotalReceiptsAffected = distinctReceipts,
            Rows = reportRows
        };
    }
}
