using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Implements document-to-document conversion along the sales pipeline.
/// Mirrors ERPNext's "Make Sales Order", "Make Delivery Note", "Make Invoice" buttons.
/// </summary>
[Authorize(MyERPPermissions.SalesOrders.Default)]
public class DocumentConversionAppService : ApplicationService, IDocumentConversionAppService
{
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentActivityLogService _activityLog;

    public DocumentConversionAppService(
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentActivityLogService activityLog)
    {
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
        _activityLog = activityLog;
    }

    private async Task<string?> ResolveCustomerNameAsync(Guid customerId)
    {
        var customer = await _customerRepository.FindAsync(customerId);
        return customer?.Name;
    }

    /// <summary>
    /// Per ERPNext get_returned_qty_map(): returns a map of {dn_detail_id: returned_qty}
    /// from submitted return Delivery Notes referencing this DN.
    /// Return DN items have negative qty; we use ABS for the returned amount.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> GetReturnedQtyMapAsync(Guid deliveryNoteId)
    {
        var result = new Dictionary<Guid, decimal>();

        var queryable = await _deliveryNoteRepository.GetQueryableAsync();
        var returnDns = queryable
            .Where(dn => dn.IsReturn && dn.ReturnAgainstId == deliveryNoteId
                      && dn.Status != Core.DocumentStatus.Draft
                      && dn.Status != Core.DocumentStatus.Cancelled)
            .ToList();

        foreach (var returnDn in returnDns)
        {
            foreach (var item in returnDn.Items)
            {
                // Return items have negative qty; use absolute value for deduction
                var absQty = Math.Abs(item.Quantity);
                if (item.SalesOrderItemId.HasValue)
                {
                    // Map by the original DN item this return targets
                    // Use SalesOrderItemId as proxy — in ERPNext uses dn_detail field
                    var key = item.SalesOrderItemId.Value;
                    result[key] = result.GetValueOrDefault(key, 0m) + absQty;
                }
            }
        }

        return result;
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<SalesOrderDto> ConvertQuotationToSalesOrderAsync(Guid quotationId)
    {
        var quotation = await _quotationRepository.GetAsync(quotationId);

        if (quotation.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (quotation.ConvertedToSalesOrderId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "Quotation")
                .WithData("documentNumber", quotation.QuotationNumber)
                .WithData("reason", "This quotation has already been converted to a Sales Order");

        // Block conversion of expired quotations
        if (quotation.IsExpired)
            throw new BusinessException("MyERP:07003")
                .WithData("quotationNumber", quotation.QuotationNumber)
                .WithData("validUntil", quotation.ValidUntil?.ToString("dd/MM/yyyy") ?? "");

        var orderNumber = await _numberGenerator.GenerateAsync("SalesOrder", quotation.CompanyId);

        var salesOrder = new SalesOrder(
            GuidGenerator.Create(),
            quotation.CompanyId,
            quotation.CustomerId,
            orderNumber,
            Clock.Now.Date,
            quotation.TenantId);

        salesOrder.QuotationId = quotation.Id;
        salesOrder.CurrencyCode = quotation.CurrencyCode;
        salesOrder.Terms = quotation.Terms;
        salesOrder.Notes = quotation.Notes;
        salesOrder.PriceListId = quotation.PriceListId;

        foreach (var item in quotation.Items)
        {
            salesOrder.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, quotationItemId: item.Id);
        }

        quotation.ConvertedToSalesOrderId = salesOrder.Id;

        await _salesOrderRepository.InsertAsync(salesOrder, autoSave: true);
        await _quotationRepository.UpdateAsync(quotation, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("Quotation", quotation.Id, quotation.CompanyId,
            "SalesOrder", salesOrder.Id, quotation.QuotationNumber, quotation.TenantId);

        var soDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(salesOrder);
        soDto.CustomerName = await ResolveCustomerNameAsync(salesOrder.CustomerId);
        return soDto;
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteAsync(Guid salesOrderId, List<PartialDeliveryItemDto>? selectedItems = null)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);

        if (salesOrder.Status == Core.DocumentStatus.Draft || salesOrder.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var deliveryNumber = await _numberGenerator.GenerateAsync("DeliveryNote", salesOrder.CompanyId);

        var deliveryNote = new DeliveryNote(
            GuidGenerator.Create(),
            salesOrder.CompanyId,
            salesOrder.CustomerId,
            salesOrder.Items.FirstOrDefault(i => i.WarehouseId.HasValue)?.WarehouseId
                ?? throw new BusinessException("MyERP:01007")
                    .WithData("documentType", "Delivery Note — no warehouse set on Sales Order items"),
            deliveryNumber,
            Clock.Now.Date,
            salesOrder.TenantId);

        deliveryNote.SalesOrderId = salesOrder.Id;
        deliveryNote.CurrencyCode = salesOrder.CurrencyCode;

        if (selectedItems is { Count: > 0 })
        {
            var soItemMap = salesOrder.Items.ToDictionary(i => i.Id);
            var mappedQtyByItem = new Dictionary<Guid, decimal>();
            foreach (var sel in selectedItems)
            {
                if (!soItemMap.TryGetValue(sel.SalesOrderItemId, out var soItem)) continue;
                var alreadyMapped = mappedQtyByItem.GetValueOrDefault(sel.SalesOrderItemId, 0m);
                var remainingPending = Math.Max(0, soItem.PendingDeliveryQty - alreadyMapped);
                var deliverQty = Math.Min(sel.Quantity, remainingPending);
                if (deliverQty <= 0) continue;
                deliveryNote.AddItem(soItem.ItemId, soItem.Description, deliverQty, soItem.UnitPrice, soItem.TaxAmount, soItem.Uom, soItem.Id);
                mappedQtyByItem[sel.SalesOrderItemId] = alreadyMapped + deliverQty;
                var lastItem = deliveryNote.Items[^1];
                lastItem.StockUom = soItem.StockUom;
                lastItem.ConversionFactor = soItem.ConversionFactor;
                lastItem.WarehouseId = sel.WarehouseId ?? soItem.WarehouseId;
            }
        }
        else
        {
            foreach (var item in salesOrder.Items)
            {
                if (item.DeliveredBySupplier || item.SkipDelivery) continue;
                var pendingQty = item.PendingDeliveryQty;
                if (pendingQty > 0)
                {
                    deliveryNote.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom, item.Id);
                    var lastItem = deliveryNote.Items[^1];
                    lastItem.StockUom = item.StockUom;
                    lastItem.ConversionFactor = item.ConversionFactor;
                    lastItem.WarehouseId = item.WarehouseId;
                }
            }
        }

        if (deliveryNote.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber ?? "")
                .WithData("reason", "No items have pending delivery quantity.");

        await _deliveryNoteRepository.InsertAsync(deliveryNote, autoSave: true);

        await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
            "DeliveryNote", deliveryNote.Id, salesOrder.OrderNumber, salesOrder.TenantId);

        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(deliveryNote);
    }

    /// <summary>
    /// Creates a Delivery Note from SO items with delivery date on or before the cutoff.
    /// Per ERPNext SO→DN mapper: `until_delivery_date` filters which items get delivered.
    /// Enables scheduled partial deliveries (deliver only items due this week/month).
    /// Drop-ship items (DeliveredBySupplier=true) are always excluded per ERPNext condition.
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteByDateAsync(Guid salesOrderId, DateTime untilDeliveryDate)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);

        if (salesOrder.Status == Core.DocumentStatus.Draft || salesOrder.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var deliveryNumber = await _numberGenerator.GenerateAsync("DeliveryNote", salesOrder.CompanyId);

        var warehouseId = salesOrder.Items
            .Where(i => i.WarehouseId.HasValue && !i.DeliveredBySupplier && !i.SkipDelivery)
            .Select(i => i.WarehouseId!.Value)
            .FirstOrDefault();

        if (warehouseId == default)
            throw new BusinessException("MyERP:01007")
                .WithData("documentType", "Delivery Note — no warehouse set on eligible Sales Order items");

        var deliveryNote = new DeliveryNote(
            GuidGenerator.Create(),
            salesOrder.CompanyId,
            salesOrder.CustomerId,
            warehouseId,
            deliveryNumber,
            Clock.Now.Date,
            salesOrder.TenantId);

        deliveryNote.SalesOrderId = salesOrder.Id;
        deliveryNote.CurrencyCode = salesOrder.CurrencyCode;

        foreach (var item in salesOrder.Items)
        {
            // Per ERPNext: exclude drop-ship items (delivered by supplier directly) and service items marked skip delivery
            if (item.DeliveredBySupplier || item.SkipDelivery) continue;

            // Per ERPNext: delivery date cutoff filter
            // Item-level delivery_date takes precedence; falls back to parent SO delivery_date
            var itemDeliveryDate = item.DeliveryDate ?? salesOrder.DeliveryDate;
            if (itemDeliveryDate.HasValue && itemDeliveryDate.Value.Date > untilDeliveryDate.Date)
                continue;

            var pendingQty = item.PendingDeliveryQty;
            if (pendingQty <= 0) continue;

            deliveryNote.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom, item.Id);
            var lastItem = deliveryNote.Items[^1];
            lastItem.StockUom = item.StockUom;
            lastItem.ConversionFactor = item.ConversionFactor;
        }

        if (deliveryNote.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber ?? "")
                .WithData("reason", $"No items with delivery date on or before {untilDeliveryDate:yyyy-MM-dd} have pending delivery.");

        await _deliveryNoteRepository.InsertAsync(deliveryNote, autoSave: true);

        await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
            "DeliveryNote", deliveryNote.Id, salesOrder.OrderNumber, salesOrder.TenantId);

        return ObjectMapper.Map<DeliveryNote, DeliveryNoteDto>(deliveryNote);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> ConvertSalesOrderToSalesInvoiceAsync(Guid salesOrderId)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);

        if (salesOrder.Status == Core.DocumentStatus.Draft || salesOrder.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", salesOrder.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            salesOrder.CompanyId,
            salesOrder.CustomerId,
            invoiceNumber,
            Clock.Now.Date,
            salesOrder.TenantId);

        invoice.CurrencyCode = salesOrder.CurrencyCode;
        invoice.Notes = salesOrder.Notes;

        // Account for draft Sales Invoices in the system (per ERPNext PR #58617)
        var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var draftInvoices = siQuery
            .Where(si => si.Status == Core.DocumentStatus.Draft)
            .SelectMany(si => si.Items)
            .Where(i => i.SalesOrderItemId.HasValue)
            .GroupBy(i => i.SalesOrderItemId!.Value)
            .Select(g => new { SoItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToList();
        var draftMappedQtyByItem = draftInvoices.ToDictionary(x => x.SoItemId, x => x.Qty);

        foreach (var item in salesOrder.Items)
        {
            // Only bill pending qty minus any draft mapped qty (per ERPNext PR #58617)
            var draftQty = draftMappedQtyByItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = Math.Max(0, item.PendingBillingQty - draftQty);
            if (pendingQty > 0)
            {
                invoice.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom);
                var lastItem = invoice.Items.Last();
                lastItem.SalesOrderItemId = item.Id;
                lastItem.StockUom = item.StockUom;
                lastItem.ConversionFactor = item.ConversionFactor;
            }
        }

        if (invoice.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber ?? "")
                .WithData("reason", "All items in this Sales Order have been fully billed or have pending draft invoices.");

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
            "SalesInvoice", invoice.Id, salesOrder.OrderNumber, salesOrder.TenantId);

        var siDto1 = ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
        siDto1.CustomerName = await ResolveCustomerNameAsync(invoice.CustomerId);
        return siDto1;
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> ConvertDeliveryNoteToSalesInvoiceAsync(Guid deliveryNoteId)
    {
        var deliveryNote = await _deliveryNoteRepository.GetAsync(deliveryNoteId);

        if (deliveryNote.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        // Per ERPNext DN→SI mapper: pending = qty - invoiced_qty - returned_qty - draft_mapped_qty
        // Get returned qty per DN item (from return DNs referencing this DN)
        var returnedQtyMap = await GetReturnedQtyMapAsync(deliveryNoteId);

        // Account for draft Sales Invoices in the system (per ERPNext PR #58617)
        var siQuery2 = await _salesInvoiceRepository.GetQueryableAsync();
        var draftInvoices2 = siQuery2
            .Where(si => si.Status == Core.DocumentStatus.Draft)
            .SelectMany(si => si.Items)
            .Where(i => i.DeliveryNoteItemId.HasValue)
            .GroupBy(i => i.DeliveryNoteItemId!.Value)
            .Select(g => new { DnItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToList();
        var draftMappedQtyByItem = draftInvoices2.ToDictionary(x => x.DnItemId, x => x.Qty);

        // Guard: check pending billing qty per DN item to prevent double-billing
        var hasConvertibleItems = false;
        foreach (var item in deliveryNote.Items)
        {
            var returnedQty = returnedQtyMap.GetValueOrDefault(item.Id, 0m);
            var draftQty = draftMappedQtyByItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = item.Quantity - item.BilledQty - returnedQty - draftQty;
            if (pendingQty > 0)
            {
                hasConvertibleItems = true;
                break;
            }
        }
        if (!hasConvertibleItems)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "DeliveryNote")
                .WithData("documentNumber", deliveryNote.DeliveryNumber)
                .WithData("reason", "All items in this Delivery Note have been fully invoiced, returned, or have pending draft invoices.");

        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", deliveryNote.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            deliveryNote.CompanyId,
            deliveryNote.CustomerId,
            invoiceNumber,
            Clock.Now.Date,
            deliveryNote.TenantId);

        invoice.CurrencyCode = deliveryNote.CurrencyCode;

        foreach (var item in deliveryNote.Items)
        {
            // Per ERPNext: pending = qty - invoiced_qty - returned_qty - draft_qty
            var returnedQty = returnedQtyMap.GetValueOrDefault(item.Id, 0m);
            var draftQty = draftMappedQtyByItem.GetValueOrDefault(item.Id, 0m);
            var billingQty = item.Quantity - item.BilledQty - returnedQty - draftQty;
            if (billingQty <= 0) continue;

            invoice.AddItem(item.ItemId, item.Description, billingQty, item.UnitPrice, item.TaxAmount, item.Uom);
            var lastItem = invoice.Items.Last();
            lastItem.SalesOrderItemId = item.SalesOrderItemId;
            lastItem.DeliveryNoteItemId = item.Id; // Track which DN item is being billed
            lastItem.StockUom = item.StockUom;
            lastItem.ConversionFactor = item.ConversionFactor;
        }

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("DeliveryNote", deliveryNote.Id, deliveryNote.CompanyId,
            "SalesInvoice", invoice.Id, deliveryNote.DeliveryNumber, deliveryNote.TenantId);

        var siDto2 = ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
        siDto2.CustomerName = await ResolveCustomerNameAsync(invoice.CustomerId);
        return siDto2;
    }

    /// <summary>
    /// Converts Sales Order items to a Material Request (type=Purchase).
    /// Per ERPNext SO→MR: delivery_date → schedule_date rename, qty → unfulfilled qty.
    /// </summary>
    [Authorize(MyERPPermissions.SalesOrders.Default)]
    public async Task<Guid> ConvertSalesOrderToMaterialRequestAsync(Guid salesOrderId)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);
        if (salesOrder.Status == Core.DocumentStatus.Draft || salesOrder.Status == Core.DocumentStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion)
                .WithData("documentType", "SalesOrder");
        }

        var mrRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.MaterialRequest, Guid>>();
        var mrNumber = await _numberGenerator.GenerateAsync("MR", salesOrder.CompanyId);

        var mr = new Purchasing.Entities.MaterialRequest(
            GuidGenerator.Create(), salesOrder.CompanyId, mrNumber,
            Purchasing.MaterialRequestType.Purchase, DateTime.UtcNow, salesOrder.TenantId)
        {
            ProjectId = salesOrder.ProjectId,
        };

        foreach (var item in salesOrder.Items.Where(i => i.PendingDeliveryQty > 0))
        {
            var pendingToRequest = Math.Max(0, item.PendingDeliveryQty - item.RequestedQty);
            if (pendingToRequest <= 0) continue;

            mr.AddItem(
                item.ItemId,
                item.Description ?? string.Empty,
                pendingToRequest,
                item.Uom ?? "Unit",
                warehouseId: item.WarehouseId,
                salesOrderId: salesOrder.Id,
                salesOrderItemId: item.Id,
                projectId: salesOrder.ProjectId,
                conversionFactor: item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
        }

        if (!mr.Items.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber)
                .WithData("reason", "All items in this Sales Order have been fully requested or delivered. No pending items for material request.");
        }

        await mrRepo.InsertAsync(mr, autoSave: true);

        await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
            "MaterialRequest", mr.Id, salesOrder.OrderNumber, salesOrder.TenantId);

        return mr.Id;
    }

    /// <summary>
    /// Creates a Quotation from an Opportunity (CRM → Sales pipeline).
    /// Per ERPNext: Opportunity "Make Quotation" copies customer, items (if any),
    /// and opportunity amount. Marks opportunity status as "Quotation".
    /// This completes: Lead → Opportunity → Quotation → SO → DN → SI → Payment
    /// </summary>
    [Authorize(MyERPPermissions.Quotations.Create)]
    public async Task<QuotationDto> ConvertOpportunityToQuotationAsync(Guid opportunityId)
    {
        var oppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Opportunity, Guid>>();
        var opp = await oppRepo.GetAsync(opportunityId);

        if (opp.Status is not (OpportunityStatus.Open or OpportunityStatus.Replied))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Opportunity must be Open or Replied to create a Quotation");

        if (!opp.CustomerId.HasValue)
            throw new BusinessException("MyERP:05010")
                .WithData("detail", "Opportunity must have a Customer/Lead to create a Quotation");

        // Per ERPNext: check if quotation already exists for this opportunity
        var existingQuery = await _quotationRepository.GetQueryableAsync();
        var alreadyExists = existingQuery.Any(q =>
            q.OpportunityId == opportunityId &&
            q.Status != Core.DocumentStatus.Cancelled);
        if (alreadyExists)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("reason", "Quotation already exists for this Opportunity");

        var quotationNumber = await _numberGenerator.GenerateAsync("Quotation", opp.CompanyId);

        var quotation = new Quotation(
            GuidGenerator.Create(),
            opp.CompanyId,
            opp.CustomerId.Value,
            quotationNumber,
            Clock.Now.Date,
            opp.TenantId);

        quotation.OpportunityId = opp.Id;

        // Copy items from opportunity (if any)
        if (opp.Items.Any())
        {
            foreach (var item in opp.Items)
            {
                quotation.AddItem(
                    item.ItemId ?? Guid.Empty,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    0m, // taxAmount — resolved later on quotation
                    item.Uom ?? "Unit");
            }
        }
        else if (opp.OpportunityAmount > 0)
        {
            // No items but has an amount — create a single line item
            quotation.AddItem(
                Guid.Empty, // placeholder — user will select item
                opp.Title ?? "Opportunity Item",
                1,
                opp.OpportunityAmount,
                0m,
                "Unit");
        }

        await _quotationRepository.InsertAsync(quotation, autoSave: true);

        // Mark opportunity as Quotation status
        opp.MarkQuotation();
        await oppRepo.UpdateAsync(opp, autoSave: true);

        // Per PR #57507: carry forward communications from opportunity to quotation
        // This links email threads and comments from the opportunity for context continuity
        await _activityLog.LogConvertedAsync("Opportunity", opp.Id, opp.CompanyId,
            "Quotation", quotation.Id, opp.OpportunityNumber, opp.TenantId);

        return ObjectMapper.Map<Quotation, QuotationDto>(quotation);
    }
}
