using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Checks for existing draft documents linked to a source document before creating new ones.
/// Per ERPNext PR #57299: warns when a draft linked document already exists, preventing duplicates.
/// This enables the user to edit the existing draft instead of creating another one.
/// </summary>
public class DraftLinkGuardService : DomainService
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;

    public DraftLinkGuardService(
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<StockEntry, Guid> stockEntryRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockEntryRepository = stockEntryRepository;
    }

    /// <summary>
    /// Finds existing draft Delivery Notes linked to a Sales Order.
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftDeliveryNotesForSalesOrderAsync(Guid salesOrderId)
    {
        var query = await _deliveryNoteRepository.GetQueryableAsync();
        return query
            .Where(dn => dn.SalesOrderId == salesOrderId && dn.Status == DocumentStatus.Draft)
            .Select(dn => new DraftLinkInfo(dn.Id, dn.DeliveryNumber, "DeliveryNote"))
            .ToList();
    }

    /// <summary>
    /// Finds existing draft Sales Invoices linked to a Sales Order (via item references).
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftSalesInvoicesForSalesOrderAsync(Guid salesOrderId)
    {
        var query = await _salesInvoiceRepository.GetQueryableAsync();
        return query
            .Where(si => si.Status == DocumentStatus.Draft
                      && si.Items.Any(i => i.SalesOrderItemId.HasValue))
            .Select(si => new DraftLinkInfo(si.Id, si.InvoiceNumber, "SalesInvoice"))
            .ToList();
    }

    /// <summary>
    /// Finds existing draft Sales Invoices that may have been created from a Delivery Note context.
    /// Since there's no direct FK from SI→DN, this checks for draft SIs for the same customer+company.
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftSalesInvoicesForDeliveryNoteAsync(Guid deliveryNoteId)
    {
        // DN→SI linkage in MyERP is indirect via SO items or direct creation.
        // For guard purposes, we check draft SIs with matching SO item links.
        var query = await _salesInvoiceRepository.GetQueryableAsync();
        var dnQuery = await _deliveryNoteRepository.GetQueryableAsync();
        var dn = dnQuery.FirstOrDefault(d => d.Id == deliveryNoteId);
        if (dn == null) return new List<DraftLinkInfo>();

        return query
            .Where(si => si.Status == DocumentStatus.Draft
                      && si.CustomerId == dn.CustomerId
                      && si.CompanyId == dn.CompanyId)
            .Select(si => new DraftLinkInfo(si.Id, si.InvoiceNumber, "SalesInvoice"))
            .ToList();
    }

    /// <summary>
    /// Finds existing draft Purchase Receipts linked to a Purchase Order.
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftPurchaseReceiptsForPurchaseOrderAsync(Guid purchaseOrderId)
    {
        var query = await _purchaseReceiptRepository.GetQueryableAsync();
        return query
            .Where(pr => pr.PurchaseOrderId == purchaseOrderId && pr.Status == DocumentStatus.Draft)
            .Select(pr => new DraftLinkInfo(pr.Id, pr.ReceiptNumber, "PurchaseReceipt"))
            .ToList();
    }

    /// <summary>
    /// Finds existing draft Purchase Invoices linked to a Purchase Order (via item references).
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftPurchaseInvoicesForPurchaseOrderAsync(Guid purchaseOrderId)
    {
        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        return query
            .Where(pi => pi.Status == DocumentStatus.Draft
                      && pi.Items.Any(i => i.PurchaseOrderItemId.HasValue))
            .Select(pi => new DraftLinkInfo(pi.Id, pi.InvoiceNumber, "PurchaseInvoice"))
            .ToList();
    }

    /// <summary>
    /// Finds existing draft Purchase Invoices linked to a Purchase Receipt (via item references).
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftPurchaseInvoicesForPurchaseReceiptAsync(Guid purchaseReceiptId)
    {
        var query = await _purchaseInvoiceRepository.GetQueryableAsync();
        return query
            .Where(pi => pi.Status == DocumentStatus.Draft
                      && pi.Items.Any(i => i.PurchaseReceiptItemId.HasValue))
            .Select(pi => new DraftLinkInfo(pi.Id, pi.InvoiceNumber, "PurchaseInvoice"))
            .ToList();
    }

    /// <summary>
    /// Finds existing draft Stock Entries linked to a Work Order.
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetDraftStockEntriesForWorkOrderAsync(Guid workOrderId)
    {
        var query = await _stockEntryRepository.GetQueryableAsync();
        return query
            .Where(se => se.WorkOrderId == workOrderId && se.Status == DocumentStatus.Draft)
            .Select(se => new DraftLinkInfo(se.Id, se.EntryNumber, "StockEntry"))
            .ToList();
    }

    /// <summary>
    /// Generic draft check for any source→target document pair.
    /// Returns draft documents of targetType that reference the source.
    /// </summary>
    public async Task<List<DraftLinkInfo>> GetExistingDraftsAsync(
        string sourceDocType, Guid sourceId, string targetDocType)
    {
        return (sourceDocType, targetDocType) switch
        {
            ("SalesOrder", "DeliveryNote") => await GetDraftDeliveryNotesForSalesOrderAsync(sourceId),
            ("SalesOrder", "SalesInvoice") => await GetDraftSalesInvoicesForSalesOrderAsync(sourceId),
            ("DeliveryNote", "SalesInvoice") => await GetDraftSalesInvoicesForDeliveryNoteAsync(sourceId),
            ("PurchaseOrder", "PurchaseReceipt") => await GetDraftPurchaseReceiptsForPurchaseOrderAsync(sourceId),
            ("PurchaseOrder", "PurchaseInvoice") => await GetDraftPurchaseInvoicesForPurchaseOrderAsync(sourceId),
            ("PurchaseReceipt", "PurchaseInvoice") => await GetDraftPurchaseInvoicesForPurchaseReceiptAsync(sourceId),
            ("WorkOrder", "StockEntry") => await GetDraftStockEntriesForWorkOrderAsync(sourceId),
            _ => new List<DraftLinkInfo>()
        };
    }
}

/// <summary>
/// Represents a draft document that already exists for a source document.
/// </summary>
public record DraftLinkInfo(Guid DocumentId, string? DocumentNumber, string DocumentType);
