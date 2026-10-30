using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Inventory.Entities;
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
    /// Computes returned quantity per DN item across all submitted Return Delivery Notes referencing this DN.
    /// Per ERPNext PR #58953, PR #58869 & PR #60570 (commit 257d827f5b): enables accurate billable quantity calculation
    /// when converting DN to SI by converting return quantities from their transaction UOM into the original DN item's UOM.
    /// Maps directly to DeliveryNoteItem.Id using ReturnedQty on item and any linked return delivery note items.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> GetReturnedQtyMapAsync(DeliveryNote deliveryNote)
    {
        var result = new Dictionary<Guid, decimal>();

        foreach (var item in deliveryNote.Items)
        {
            result[item.Id] = Math.Abs(item.ReturnedQty);
        }

        var queryable = await _deliveryNoteRepository.GetQueryableAsync();
        var returnDns = queryable
            .Where(dn => dn.IsReturn && dn.ReturnAgainstId == deliveryNote.Id
                      && dn.Status != Core.DocumentStatus.Draft
                      && dn.Status != Core.DocumentStatus.Cancelled)
            .ToList();

        var queriedReturns = new Dictionary<Guid, decimal>();
        foreach (var returnDn in returnDns)
        {
            foreach (var item in returnDn.Items)
            {
                var origItem = deliveryNote.Items.FirstOrDefault(i =>
                    (item.SalesOrderItemId.HasValue && i.SalesOrderItemId == item.SalesOrderItemId) ||
                    (!item.SalesOrderItemId.HasValue && i.ItemId == item.ItemId));
                if (origItem != null)
                {
                    // Per ERPNext PR #60570: convert returned qty from return UOM to original DN item UOM
                    var returnFactor = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
                    var origFactor = origItem.ConversionFactor > 0 ? origItem.ConversionFactor : 1m;
                    var returnedInOrigUom = (Math.Abs(item.Quantity) * returnFactor) / origFactor;
                    queriedReturns[origItem.Id] = queriedReturns.GetValueOrDefault(origItem.Id, 0m) + returnedInOrigUom;
                }
            }
        }

        foreach (var item in deliveryNote.Items)
        {
            result[item.Id] = Math.Max(result.GetValueOrDefault(item.Id, 0m), queriedReturns.GetValueOrDefault(item.Id, 0m));
        }

        return result;
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<SalesOrderDto> ConvertQuotationToSalesOrderAsync(Guid quotationId, List<Guid>? selectedItemIds = null)
    {
        var quotation = await _quotationRepository.GetAsync(quotationId);

        if (quotation.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        // Per ERPNext PR #59378 (commit 4f1f676b24): block conversion of inactive or lost quotations
        if (!quotation.IsActive)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot convert an inactive Quotation version to Sales Order.");

        if (quotation.Status == Core.DocumentStatus.Rejected)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot convert a lost Quotation to Sales Order.");

        if (quotation.ConvertedToSalesOrderId.HasValue || quotation.IsFullyOrdered)
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

        // Auto-fill addresses from customer master per ERPNext (PR #59336)
        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddress = await partyDefaults.GetPrimaryAddressAsync("Customer", quotation.CustomerId);
        if (billingAddress != null) salesOrder.BillingAddressId = billingAddress.Id;
        var shippingAddress = await partyDefaults.GetShippingAddressAsync("Customer", quotation.CustomerId);
        if (shippingAddress != null) salesOrder.ShippingAddressId = shippingAddress.Id;

        // Alternative offers are not ordered unless the user picks them explicitly (ERPNext); at most
        // one row of each alternatives set (a row plus the alternative rows right after it) may be ordered.
        IEnumerable<QuotationItem> itemsToOrder;
        if (selectedItemIds is { Count: > 0 })
        {
            var unknown = selectedItemIds.FirstOrDefault(id => quotation.Items.All(i => i.Id != id));
            if (unknown != default)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Item {unknown} does not belong to Quotation {quotation.QuotationNumber}.");

            var setStart = 0;
            for (var idx = 0; idx < quotation.Items.Count; idx++)
            {
                if (!quotation.Items[idx].IsAlternative)
                    setStart = idx;
                if (idx == quotation.Items.Count - 1 || !quotation.Items[idx + 1].IsAlternative)
                {
                    var picked = quotation.Items.Skip(setStart).Take(idx - setStart + 1).Count(i => selectedItemIds.Contains(i.Id));
                    if (picked > 1)
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", "Only one item of an alternatives set can be ordered.");
                }
            }

            itemsToOrder = quotation.Items.Where(i => selectedItemIds.Contains(i.Id));
        }
        else
        {
            itemsToOrder = quotation.Items.Where(i => !i.IsAlternative);
        }

        foreach (var item in itemsToOrder)
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

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> ConvertQuotationToSalesInvoiceAsync(Guid quotationId, List<Guid>? selectedItemIds = null)
    {
        var quotation = await _quotationRepository.GetAsync(quotationId);

        // Per ERPNext PR #62623 (commit bddb16e1a3): block invoicing inactive, lost, or expired quotations
        if (!quotation.IsActive)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot create Sales Invoice from inactive Quotation {quotation.QuotationNumber}.");

        if (quotation.Status == Core.DocumentStatus.Rejected || quotation.OrderStatus == "Lost")
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot create Sales Invoice from Lost Quotation {quotation.QuotationNumber}.");

        if (quotation.IsExpired)
            throw new BusinessException("MyERP:07003")
                .WithData("quotationNumber", quotation.QuotationNumber)
                .WithData("validUntil", quotation.ValidUntil?.ToString("dd/MM/yyyy") ?? "");

        if (quotation.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", quotation.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            quotation.CompanyId,
            quotation.CustomerId,
            invoiceNumber,
            Clock.Now.Date,
            quotation.TenantId);

        invoice.CurrencyCode = quotation.CurrencyCode;
        invoice.Terms = quotation.Terms;
        invoice.Notes = quotation.Notes;
        invoice.PriceListId = quotation.PriceListId;

        var partyDefaults = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.PartyDefaultsService>();
        var billingAddress = await partyDefaults.GetPrimaryAddressAsync("Customer", quotation.CustomerId);
        if (billingAddress != null) invoice.BillingAddressId = billingAddress.Id;
        var shippingAddress = await partyDefaults.GetShippingAddressAsync("Customer", quotation.CustomerId);
        if (shippingAddress != null) invoice.ShippingAddressId = shippingAddress.Id;

        IEnumerable<QuotationItem> itemsToInvoice;
        if (selectedItemIds is { Count: > 0 })
        {
            var unknown = selectedItemIds.FirstOrDefault(id => quotation.Items.All(i => i.Id != id));
            if (unknown != default)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Item {unknown} does not belong to Quotation {quotation.QuotationNumber}.");

            var setStart = 0;
            for (var idx = 0; idx < quotation.Items.Count; idx++)
            {
                if (!quotation.Items[idx].IsAlternative)
                    setStart = idx;
                if (idx == quotation.Items.Count - 1 || !quotation.Items[idx + 1].IsAlternative)
                {
                    var picked = quotation.Items.Skip(setStart).Take(idx - setStart + 1).Count(i => selectedItemIds.Contains(i.Id));
                    if (picked > 1)
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", "Only one item of an alternatives set can be ordered.");
                }
            }

            itemsToInvoice = quotation.Items.Where(i => selectedItemIds.Contains(i.Id));
        }
        else
        {
            itemsToInvoice = quotation.Items.Where(i => !i.IsAlternative);
        }

        foreach (var item in itemsToInvoice)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            var last = invoice.Items[^1];
            last.StockUom = item.StockUom;
            last.ConversionFactor = item.ConversionFactor;
        }

        if (invoice.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "No items to convert into Sales Invoice.");

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        await _activityLog.LogConvertedAsync("Quotation", quotation.Id, quotation.CompanyId,
            "SalesInvoice", invoice.Id, quotation.QuotationNumber, quotation.TenantId);

        var siDto = ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
        siDto.CustomerName = await ResolveCustomerNameAsync(invoice.CustomerId);
        return siDto;
    }

    private async Task<(decimal AllowancePct, Dictionary<string, bool> WholeNumberUoms)> GetDeliveryAllowanceContextAsync(SalesOrder salesOrder)
    {
        var companyRepo = LazyServiceProvider.LazyGetService<IRepository<Company, Guid>>();
        var allowancePct = 0m;
        if (companyRepo != null)
        {
            var company = await companyRepo.FindAsync(salesOrder.CompanyId);
            if (company != null)
            {
                allowancePct = company.OverDeliveryReceiptAllowance;
            }
        }

        var uomRepo = LazyServiceProvider.LazyGetService<IRepository<Uom, Guid>>();
        Dictionary<string, bool> wholeNumberUoms = new(StringComparer.OrdinalIgnoreCase);
        if (uomRepo != null)
        {
            var uomNames = salesOrder.Items.Select(i => i.Uom).Distinct().ToList();
            var uomQuery = await uomRepo.GetQueryableAsync();
            if (uomQuery != null)
            {
                var filtered = uomQuery.Where(u => uomNames.Contains(u.Name));
                var uomList = AsyncExecuter != null
                    ? await AsyncExecuter.ToListAsync(filtered)
                    : filtered.ToList();
                if (uomList != null)
                {
                    wholeNumberUoms = uomList
                        .GroupBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().MustBeWholeNumber, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        return (allowancePct, wholeNumberUoms);
    }

    private static decimal CalculateRemainingDeliverableQty(
        SalesOrderItem item,
        decimal alreadyMapped,
        bool isSelectedRow,
        bool canOverDeliverAllRows,
        decimal allowancePct,
        IReadOnlyDictionary<string, bool> wholeNumberUoms)
    {
        var deliveredQty = item.DeliveredQty + alreadyMapped;
        var canOverDeliver = (canOverDeliverAllRows || isSelectedRow) && allowancePct > 0;

        if (deliveredQty < item.Quantity)
        {
            if (isSelectedRow && canOverDeliver)
            {
                var isWhole = wholeNumberUoms.GetValueOrDefault(item.Uom, false);
                var maxAllowed = item.GetMaxDeliverableQty(allowancePct, isWhole);
                return Math.Max(0, Math.Round(maxAllowed - deliveredQty, 4));
            }
            return Math.Max(0, Math.Round(item.Quantity - deliveredQty, 4));
        }

        if (!canOverDeliver)
        {
            return 0m;
        }

        var isWholeNumber = wholeNumberUoms.GetValueOrDefault(item.Uom, false);
        var maxAllowedOver = item.GetMaxDeliverableQty(allowancePct, isWholeNumber);
        return Math.Max(0, Math.Round(maxAllowedOver - deliveredQty, 4));
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

        var (allowancePct, wholeNumberUoms) = await GetDeliveryAllowanceContextAsync(salesOrder);
        var canOverDeliverAllRows = salesOrder.PerDelivered >= 100m;

        if (selectedItems is { Count: > 0 })
        {
            var soItemMap = salesOrder.Items.ToDictionary(i => i.Id);
            var mappedQtyByItem = new Dictionary<Guid, decimal>();
            foreach (var sel in selectedItems)
            {
                if (!soItemMap.TryGetValue(sel.SalesOrderItemId, out var soItem))
                {
                    soItem = salesOrder.Items.FirstOrDefault(i => i.ItemId == sel.SalesOrderItemId);
                }
                if (soItem == null) continue;
                if (soItem.DeliveredBySupplier || soItem.SkipDelivery || soItem.IsClosed) continue;

                var alreadyMapped = mappedQtyByItem.GetValueOrDefault(soItem.Id, 0m);
                var remainingPending = CalculateRemainingDeliverableQty(soItem, alreadyMapped, isSelectedRow: true, canOverDeliverAllRows, allowancePct, wholeNumberUoms);
                var deliverQty = Math.Min(sel.Quantity, remainingPending);
                if (deliverQty <= 0) continue;

                var itemTax = soItem.Quantity > 0 ? Math.Round(soItem.TaxAmount * (deliverQty / soItem.Quantity), 2) : 0m;
                deliveryNote.AddItem(soItem.ItemId, soItem.Description, deliverQty, soItem.UnitPrice, itemTax, soItem.Uom, soItem.Id);
                mappedQtyByItem[soItem.Id] = alreadyMapped + deliverQty;
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
                if (item.DeliveredBySupplier || item.SkipDelivery || item.IsClosed) continue;
                var pendingQty = CalculateRemainingDeliverableQty(item, 0m, isSelectedRow: false, canOverDeliverAllRows, allowancePct, wholeNumberUoms);
                if (pendingQty > 0)
                {
                    var itemTax = item.Quantity > 0 ? Math.Round(item.TaxAmount * (pendingQty / item.Quantity), 2) : 0m;
                    deliveryNote.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, itemTax, item.Uom, item.Id);
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

        var (allowancePct, wholeNumberUoms) = await GetDeliveryAllowanceContextAsync(salesOrder);
        var canOverDeliverAllRows = salesOrder.PerDelivered >= 100m;

        foreach (var item in salesOrder.Items)
        {
            // Per ERPNext: exclude drop-ship items (delivered by supplier directly) and service items marked skip delivery
            if (item.DeliveredBySupplier || item.SkipDelivery || item.IsClosed) continue;

            // Per ERPNext: delivery date cutoff filter
            // Item-level delivery_date takes precedence; falls back to parent SO delivery_date
            var itemDeliveryDate = item.DeliveryDate ?? salesOrder.DeliveryDate;
            if (itemDeliveryDate.HasValue && itemDeliveryDate.Value.Date > untilDeliveryDate.Date)
                continue;

            var pendingQty = CalculateRemainingDeliverableQty(item, 0m, isSelectedRow: false, canOverDeliverAllRows, allowancePct, wholeNumberUoms);
            if (pendingQty <= 0) continue;

            var itemTax = item.Quantity > 0 ? Math.Round(item.TaxAmount * (pendingQty / item.Quantity), 2) : 0m;
            deliveryNote.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, itemTax, item.Uom, item.Id);
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

        // Per ERPNext PR #58966 / PR #58816: exclude fully billed orders
        if (salesOrder.PerBilled >= 100m && salesOrder.Items.All(i => i.PendingBillingQty <= 0))
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber ?? "")
                .WithData("reason", "Sales Order is already fully billed.");

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
        invoice.CustomerPoNumber = salesOrder.CustomerPoNumber;
        invoice.CouponCode = salesOrder.CouponCode;

        // Account for draft Sales Invoices in the system (per ERPNext PR #58617 & PR #60570)
        var soItemIds = salesOrder.Items.Select(i => i.Id).ToList();
        var draftMappedQtyByItem = new Dictionary<Guid, decimal>();
        if (soItemIds.Count > 0)
        {
            var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
            var draftSiItems = siQuery
                .Where(si => si.Status == Core.DocumentStatus.Draft && !si.IsReturn && !si.IsDebitNote)
                .SelectMany(si => si.Items)
                .Where(i => i.SalesOrderItemId.HasValue && soItemIds.Contains(i.SalesOrderItemId.Value))
                .ToList();

            foreach (var draftItem in draftSiItems)
            {
                var soItem = salesOrder.Items.FirstOrDefault(i => i.Id == draftItem.SalesOrderItemId!.Value);
                if (soItem != null)
                {
                    var siFactor = draftItem.ConversionFactor > 0 ? draftItem.ConversionFactor : 1m;
                    var soFactor = soItem.ConversionFactor > 0 ? soItem.ConversionFactor : 1m;
                    var qtyInSoUom = (draftItem.Quantity * siFactor) / soFactor;
                    draftMappedQtyByItem[soItem.Id] = draftMappedQtyByItem.GetValueOrDefault(soItem.Id, 0m) + qtyInSoUom;
                }
            }
        }

        foreach (var item in salesOrder.Items)
        {
            // Only bill pending qty minus any draft mapped qty (per ERPNext PR #58617)
            var draftQty = draftMappedQtyByItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = Math.Max(0, item.PendingBillingQty - draftQty);
            if (pendingQty > 0)
            {
                var itemTax = item.Quantity > 0 ? Math.Round(item.TaxAmount * (pendingQty / item.Quantity), 2) : 0m;
                invoice.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, itemTax, item.Uom);
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
        var returnedQtyMap = await GetReturnedQtyMapAsync(deliveryNote);

        var dnItemIds = deliveryNote.Items.Select(i => i.Id).ToList();
        var siQuery2 = await _salesInvoiceRepository.GetQueryableAsync();

        // Account for draft Sales Invoices in the system (per ERPNext PR #58617 & PR #60570)
        var draftInvoices2 = siQuery2
            .Where(si => si.Status == Core.DocumentStatus.Draft && !si.IsReturn && !si.IsDebitNote)
            .SelectMany(si => si.Items)
            .Where(i => i.DeliveryNoteItemId.HasValue && dnItemIds.Contains(i.DeliveryNoteItemId.Value))
            .ToList();
        var draftMappedQtyByItem = new Dictionary<Guid, decimal>();
        foreach (var draftItem in draftInvoices2)
        {
            var dnItem = deliveryNote.Items.FirstOrDefault(i => i.Id == draftItem.DeliveryNoteItemId!.Value);
            if (dnItem != null)
            {
                var siFactor = draftItem.ConversionFactor > 0 ? draftItem.ConversionFactor : 1m;
                var dnFactor = dnItem.ConversionFactor > 0 ? dnItem.ConversionFactor : 1m;
                var qtyInDnUom = (draftItem.Quantity * siFactor) / dnFactor;
                draftMappedQtyByItem[dnItem.Id] = draftMappedQtyByItem.GetValueOrDefault(dnItem.Id, 0m) + qtyInDnUom;
            }
        }

        // Compute invoiced quantity map across submitted invoices & credit notes (per ERPNext PR #60570 / commit 257d827f5b)
        var submittedInvoices = siQuery2
            .Where(si => (si.Status == Core.DocumentStatus.Submitted || si.Status == Core.DocumentStatus.Posted) && !si.UpdateStock)
            .SelectMany(si => si.Items)
            .Where(i => i.DeliveryNoteItemId.HasValue && dnItemIds.Contains(i.DeliveryNoteItemId.Value))
            .ToList();
        var invoicedQtyByItem = new Dictionary<Guid, decimal>();
        foreach (var siItem in submittedInvoices)
        {
            var dnItem = deliveryNote.Items.FirstOrDefault(i => i.Id == siItem.DeliveryNoteItemId!.Value);
            if (dnItem != null)
            {
                var siFactor = siItem.ConversionFactor > 0 ? siItem.ConversionFactor : 1m;
                var dnFactor = dnItem.ConversionFactor > 0 ? dnItem.ConversionFactor : 1m;
                var qtyInDnUom = (siItem.Quantity * siFactor) / dnFactor;
                invoicedQtyByItem[dnItem.Id] = invoicedQtyByItem.GetValueOrDefault(dnItem.Id, 0m) + qtyInDnUom;
            }
        }

        // Guard: check pending billing qty per DN item to prevent double-billing
        var hasConvertibleItems = false;
        foreach (var item in deliveryNote.Items)
        {
            if (item.IsClosed) continue;
            var invoicedQty = invoicedQtyByItem.TryGetValue(item.Id, out var queriedBilled)
                ? Math.Max(0m, queriedBilled)
                : Math.Abs(item.BilledQty);
            var returnedQty = Math.Max(item.ReturnedQty, returnedQtyMap.GetValueOrDefault(item.Id, 0m));
            var draftQty = draftMappedQtyByItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = Math.Max(0, Math.Abs(item.Quantity) - invoicedQty - returnedQty - draftQty);
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
        invoice.UpdateStock = false; // Per ERPNext PR #60230 / commit 637fadfee9: disable update stock for sales invoice created from DN

        var firstSoItemId = deliveryNote.Items.FirstOrDefault(i => i.SalesOrderItemId.HasValue)?.SalesOrderItemId;
        if (firstSoItemId.HasValue)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var so = soQuery.FirstOrDefault(s => s.Items.Any(i => i.Id == firstSoItemId.Value));
            if (so != null && !string.IsNullOrWhiteSpace(so.CustomerPoNumber))
            {
                invoice.CustomerPoNumber = so.CustomerPoNumber;
            }
        }

        foreach (var item in deliveryNote.Items)
        {
            if (item.IsClosed) continue;
            // Per ERPNext: pending = qty - invoiced_qty - returned_qty - draft_qty
            var invoicedQty = invoicedQtyByItem.TryGetValue(item.Id, out var queriedBilled)
                ? Math.Max(0m, queriedBilled)
                : Math.Abs(item.BilledQty);
            var returnedQty = Math.Max(item.ReturnedQty, returnedQtyMap.GetValueOrDefault(item.Id, 0m));
            var draftQty = draftMappedQtyByItem.GetValueOrDefault(item.Id, 0m);
            var billingQty = Math.Max(0, Math.Abs(item.Quantity) - invoicedQty - returnedQty - draftQty);
            if (billingQty <= 0) continue;

            var itemTax = Math.Abs(item.Quantity) > 0 ? Math.Round(item.TaxAmount * (billingQty / Math.Abs(item.Quantity)), 2) : 0m;
            invoice.AddItem(item.ItemId, item.Description, billingQty, item.UnitPrice, itemTax, item.Uom);
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
    /// Converts a submitted Sales Order's open, non-drop-ship rows into a Draft Pick List.
    /// Per ERPNext selling/doctype/sales_order/mapper.py create_pick_list &amp; PR #62321 (commit 754475b51a):
    /// - Blocks Pick List creation if the Sales Order has active Stock Reservation Entries.
    /// - Skips closed, drop-ship (DeliveredBySupplier), SkipDelivery, and fully delivered rows for BOTH
    ///   plain items and Product Bundle packed components.
    /// - Sizes remaining qty as qty - max(picked_qty / conversion_factor, delivered_qty).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<Guid> ConvertSalesOrderToPickListAsync(Guid salesOrderId)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);
        if (salesOrder.Status == Core.DocumentStatus.Draft
            || salesOrder.Status == Core.DocumentStatus.Cancelled
            || salesOrder.Status == Core.DocumentStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion)
                .WithData("documentType", "SalesOrder");
        }

        var sreManager = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockReservationManager>();
        if (await sreManager.HasReservedStockAsync(salesOrder.Id))
        {
            throw new BusinessException("MyERP:13020")
                .WithData("reason", $"Cannot create a Pick List for Sales Order {salesOrder.OrderNumber} because it has reserved stock. Deliver directly against the reservation or unreserve first.");
        }

        var plRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PickList, Guid>>();
        var bundleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ProductBundle, Guid>>();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var uomService = LazyServiceProvider.LazyGetService<Inventory.DomainServices.UomConversionService>();

        string plNumber;
        try
        {
            plNumber = await _numberGenerator.GenerateAsync("PickList", salesOrder.CompanyId);
        }
        catch (BusinessException ex) when (ex.Code == MyERPDomainErrorCodes.DocumentSeriesNotConfigured)
        {
            plNumber = $"PL-{GuidGenerator.Create().ToString("N")[..8].ToUpperInvariant()}";
        }
        var pickList = new PickList(GuidGenerator.Create(), salesOrder.CompanyId, "Delivery", salesOrder.TenantId)
        {
            PickListNumber = plNumber,
            SalesOrderId = salesOrder.Id,
            CustomerId = salesOrder.CustomerId,
        };

        var fallbackWarehouseId = salesOrder.Items.FirstOrDefault(i => i.WarehouseId.HasValue)?.WarehouseId
            ?? (await warehouseRepo.FirstOrDefaultAsync(w => w.CompanyId == salesOrder.CompanyId && !w.IsGroup))?.Id;

        var bundleQueryable = await bundleRepo.GetQueryableAsync();
        var itemQueryable = await itemRepo.GetQueryableAsync();

        foreach (var soItem in salesOrder.Items)
        {
            // Per ERPNext commit 754475b51a (should_pick_order_item & should_pick_packed_item):
            // skip closed, drop-ship, skip-delivery, and already delivered rows (including bundles)
            if (soItem.IsClosed
                || soItem.DeliveredBySupplier
                || soItem.SkipDelivery
                || Math.Abs(soItem.DeliveredQty) >= Math.Abs(soItem.Quantity))
            {
                continue;
            }

            var rowFactor = soItem.ConversionFactor > 0 ? soItem.ConversionFactor : 1m;
            ProductBundle? bundle = null;
            if (soItem.ProductBundleId.HasValue)
            {
                bundle = bundleQueryable.FirstOrDefault(b => b.Id == soItem.ProductBundleId.Value);
            }
            bundle ??= bundleQueryable.FirstOrDefault(b => b.ItemId == soItem.ItemId && b.IsActive && !b.IsDisabled);

            var rowWarehouseId = soItem.WarehouseId ?? fallbackWarehouseId;

            if (bundle != null && bundle.Items.Count > 0)
            {
                // PickedQty on bundle SO item is recorded in bundle count (ERPNext update_packed_item_qty)
                var pickedInOrderUom = soItem.PickedQty / rowFactor;
                var pendingBundles = soItem.Quantity - Math.Max(pickedInOrderUom, soItem.DeliveredQty);
                if (pendingBundles <= 0)
                    continue;

                if (!rowWarehouseId.HasValue)
                    throw new BusinessException("MyERP:01007")
                        .WithData("documentType", "Pick List — Warehouse is required");

                var compIds = bundle.Items.Select(c => c.ComponentItemId).Distinct().ToList();
                var compItems = itemQueryable.Where(i => compIds.Contains(i.Id)).ToDictionary(i => i.Id);

                foreach (var comp in bundle.Items)
                {
                    if (comp.Qty <= 0) continue;

                    var compFactor = 1m;
                    compItems.TryGetValue(comp.ComponentItemId, out var compItem);
                    if (compItem != null && !string.IsNullOrWhiteSpace(comp.Uom) && uomService != null)
                    {
                        compFactor = await uomService.GetConversionFactorAsync(
                            compItem.Id, comp.Uom, compItem.Uom, compItem.VariantOfId);
                    }

                    var compStockQty = Math.Round(comp.Qty * compFactor * pendingBundles, 6);
                    if (compStockQty <= 0) continue;

                    pickList.AddItem(
                        comp.ComponentItemId,
                        rowWarehouseId.Value,
                        compStockQty,
                        stockQty: compStockQty,
                        itemName: comp.ItemName ?? compItem?.ItemName,
                        productBundleItemId: comp.Id,
                        sourceDocumentItemId: soItem.Id);
                }
            }
            else
            {
                var itemEntity = itemQueryable.FirstOrDefault(i => i.Id == soItem.ItemId);
                if (itemEntity != null && !itemEntity.MaintainStock)
                    continue;

                var pickedInOrderUom = soItem.PickedQty / rowFactor;
                var qtyToPick = soItem.Quantity - Math.Max(pickedInOrderUom, soItem.DeliveredQty);
                if (qtyToPick <= 0)
                    continue;

                if (!rowWarehouseId.HasValue)
                    throw new BusinessException("MyERP:01007")
                        .WithData("documentType", "Pick List — Warehouse is required");

                var stockQtyToPick = Math.Round(qtyToPick * rowFactor, 6);
                pickList.AddItem(
                    soItem.ItemId,
                    rowWarehouseId.Value,
                    stockQtyToPick,
                    stockQty: stockQtyToPick,
                    itemName: soItem.Description ?? itemEntity?.ItemName,
                    sourceDocumentItemId: soItem.Id);
            }
        }

        if (!pickList.Items.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber)
                .WithData("reason", "No pickable items remain on this Sales Order.");
        }

        await plRepo.InsertAsync(pickList, autoSave: true);

        await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
            "PickList", pickList.Id, salesOrder.OrderNumber, salesOrder.TenantId);

        return pickList.Id;
    }

    /// <summary>
    /// Creates one draft Purchase Order per supplier from the selected rows of a submitted Sales Order.
    /// Per ERPNext sales_order/mapper.py make_purchase_order: rows are matched by row id, only
    /// open, not-fully-ordered rows are mapped (qty = qty - ordered_qty / conversion_factor), rate and
    /// tax are NOT carried over (re-priced on the buying side), currency/price list come from the
    /// Supplier, and drop-ship rows keep the customer shipping address while stock rows drop the customer.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<List<Guid>> ConvertSalesOrderToPurchaseOrdersAsync(
        Guid salesOrderId, List<SalesOrderPurchaseItemSelectionDto> selectedItems)
    {
        if (selectedItems == null || selectedItems.Count == 0)
        {
            return new List<Guid>();
        }

        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);
        if (salesOrder.Status == Core.DocumentStatus.Draft || salesOrder.Status == Core.DocumentStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion)
                .WithData("documentType", "SalesOrder");
        }

        var lines = DomainServices.DropShipService.ResolvePurchaseLines(
            salesOrder, selectedItems.Select(s => (s.SalesOrderItemId, s.SupplierId)));
        if (lines.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SalesOrder")
                .WithData("documentNumber", salesOrder.OrderNumber)
                .WithData("reason", "All selected items in this Sales Order are closed or have already been fully ordered.");
        }

        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseOrder, Guid>>();
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
        var exchangeService = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.CurrencyExchangeService>();
        var bundleService = LazyServiceProvider.LazyGetService<DomainServices.ProductBundleDecompositionService>();
        var company = await companyRepo.GetAsync(salesOrder.CompanyId);

        var createdIds = new List<Guid>();
        foreach (var supplierGroup in lines.GroupBy(l => l.SupplierId))
        {
            var supplier = await supplierRepo.GetAsync(supplierGroup.Key);
            var poNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", salesOrder.CompanyId);
            var orderDate = DateTime.UtcNow.Date;

            var po = new Purchasing.Entities.PurchaseOrder(
                GuidGenerator.Create(), salesOrder.CompanyId, supplier.Id, poNumber, orderDate, salesOrder.TenantId)
            {
                ProjectId = salesOrder.ProjectId,
                CurrencyCode = supplier.DefaultCurrency ?? company.CurrencyCode,
                PriceListId = supplier.DefaultPriceListId,
            };
            po.ExchangeRate = await exchangeService.GetExchangeRateAsync(po.CurrencyCode, company.CurrencyCode, orderDate);

            foreach (var line in supplierGroup)
            {
                var soItem = line.Item;
                // ERPNext PR #61966 (commit a26ae5639f): decompose product bundle components and filter by purchase supplier
                var isBundle = bundleService != null && (soItem.ProductBundleId.HasValue || await bundleService.IsBundleItemAsync(soItem.ItemId));
                List<DomainServices.DecomposedItem>? components = null;
                if (isBundle)
                {
                    components = await bundleService!.DecomposeAsync(
                        soItem.ItemId, line.Quantity, soItem.UnitPrice,
                        soItem.WarehouseId, soItem.Id, productBundleId: soItem.ProductBundleId);
                }

                if (components != null && components.Count > 0)
                {
                    foreach (var comp in components)
                    {
                        var poItem = po.AddItem(
                            comp.ComponentItemId,
                            comp.ComponentItemName ?? string.Empty,
                            comp.Qty,
                            unitPrice: 0m,
                            taxAmount: 0m,
                            comp.Uom ?? "Unit",
                            comp.WarehouseId ?? soItem.WarehouseId,
                            projectId: salesOrder.ProjectId,
                            deliveredBySupplier: soItem.DeliveredBySupplier);
                        poItem.StockUom = comp.Uom ?? "Unit";
                        poItem.ConversionFactor = 1m;
                        poItem.ExpectedDeliveryDate = soItem.DeliveryDate ?? salesOrder.DeliveryDate;
                        poItem.SalesOrderId = salesOrder.Id;
                        poItem.SalesOrderItemId = soItem.Id;
                    }
                }
                else
                {
                    var poItem = po.AddItem(
                        soItem.ItemId,
                        soItem.Description ?? string.Empty,
                        line.Quantity,
                        unitPrice: 0m,
                        taxAmount: 0m,
                        soItem.Uom ?? "Unit",
                        soItem.WarehouseId,
                        projectId: salesOrder.ProjectId,
                        deliveredBySupplier: soItem.DeliveredBySupplier);
                    poItem.StockUom = soItem.StockUom;
                    poItem.ConversionFactor = soItem.ConversionFactor > 0 ? soItem.ConversionFactor : 1m;
                    poItem.ExpectedDeliveryDate = soItem.DeliveryDate ?? salesOrder.DeliveryDate;
                    poItem.SalesOrderId = salesOrder.Id;
                    poItem.SalesOrderItemId = soItem.Id;
                }
            }

            if (po.Items.Any(i => i.DeliveredBySupplier))
            {
                po.CustomerId = salesOrder.CustomerId;
                po.ShippingAddressId = salesOrder.ShippingAddressId;
            }

            await poRepo.InsertAsync(po, autoSave: true);
            createdIds.Add(po.Id);

            await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
                "PurchaseOrder", po.Id, salesOrder.OrderNumber, salesOrder.TenantId);
        }

        return createdIds;
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

        var customer = await _customerRepository.FindAsync(opp.CustomerId.Value);
        if (customer != null)
        {
            if (!customer.IsActive)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PartyDisabled)
                    .WithData("partyType", "Customer")
                    .WithData("partyName", customer.Name);
            }
            if (customer.CompanyId != opp.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("customerCompany", customer.CompanyId)
                    .WithData("opportunityCompany", opp.CompanyId);
            }
        }

        // Per ERPNext PR #60227 (commit 2e7807e09c): refuse quotations for disabled leads
        if (opp.LeadId.HasValue)
        {
            var leadRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Lead, Guid>>();
            var lead = await leadRepo.FindAsync(opp.LeadId.Value);
            if (lead != null && lead.Disabled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.LeadIsDisabled)
                    .WithData("0", lead.LeadNumber)
                    .WithData("1", "Quotation");
            }
        }

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
