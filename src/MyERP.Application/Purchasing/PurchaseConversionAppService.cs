using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.Purchasing;

/// <summary>
/// Implements document-to-document conversion along the purchase pipeline.
/// Mirrors ERPNext's "Make Purchase Receipt", "Make Purchase Invoice" buttons.
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class PurchaseConversionAppService : ApplicationService, IPurchaseConversionAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IRepository<RequestForQuotation, Guid> _rfqRepository;
    private readonly IRepository<SupplierQuotation, Guid> _sqRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentActivityLogService _activityLog;

    public PurchaseConversionAppService(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<RequestForQuotation, Guid> rfqRepository,
        IRepository<SupplierQuotation, Guid> sqRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentActivityLogService activityLog)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _materialRequestRepository = materialRequestRepository;
        _rfqRepository = rfqRepository;
        _sqRepository = sqRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
        _numberGenerator = numberGenerator;
        _activityLog = activityLog;
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<PurchaseReceiptDto> ConvertPurchaseOrderToReceiptAsync(Guid purchaseOrderId)
    {
        var po = await _purchaseOrderRepository.GetAsync(purchaseOrderId);

        if (po.Status == Core.DocumentStatus.Draft || po.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var receiptNumber = await _numberGenerator.GenerateAsync("PurchaseReceipt", po.CompanyId);

        var receipt = new PurchaseReceipt(
            GuidGenerator.Create(),
            po.CompanyId,
            po.SupplierId,
            po.Items.FirstOrDefault(i => i.WarehouseId.HasValue)?.WarehouseId
                ?? throw new BusinessException("MyERP:01007")
                    .WithData("documentType", "Purchase Receipt — no warehouse set on Purchase Order items"),
            receiptNumber,
            Clock.Now.Date,
            po.TenantId);

        receipt.PurchaseOrderId = po.Id;
        receipt.CurrencyCode = po.CurrencyCode;

        // Deduct quantities already mapped in draft Purchase Receipts (per ERPNext PR #58617)
        var prQuery = await _purchaseReceiptRepository.GetQueryableAsync();
        var draftReceipts = prQuery
            .Where(pr => pr.Status == Core.DocumentStatus.Draft)
            .SelectMany(pr => pr.Items)
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .GroupBy(i => i.PurchaseOrderItemId!.Value)
            .Select(g => new { PoItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToList();
        var draftReceiptQtyByItem = draftReceipts.ToDictionary(x => x.PoItemId, x => x.Qty);

        foreach (var item in po.Items)
        {
            // Only convert pending receipt qty minus draft mapped qty (supports partial receipts)
            var draftQty = draftReceiptQtyByItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = Math.Max(0, item.PendingReceiptQty - draftQty);
            if (pendingQty > 0)
            {
                receipt.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom, item.Id);
                // Carry forward UOM conversion data from PO item
                var lastItem = receipt.Items[^1];
                lastItem.StockUom = item.StockUom;
                lastItem.ConversionFactor = item.ConversionFactor;
                // Propagate per-item warehouse override
                if (item.WarehouseId.HasValue)
                    lastItem.WarehouseId = item.WarehouseId;
            }
        }

        if (receipt.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "PurchaseOrder")
                .WithData("documentNumber", po.OrderNumber)
                .WithData("reason", "All items in this Purchase Order have been fully received or have pending draft receipts.");

        await _purchaseReceiptRepository.InsertAsync(receipt, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("PurchaseOrder", po.Id, po.CompanyId,
            "PurchaseReceipt", receipt.Id, po.OrderNumber, po.TenantId);

        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(receipt);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<PurchaseInvoiceDto> ConvertPurchaseOrderToInvoiceAsync(Guid purchaseOrderId)
    {
        var po = await _purchaseOrderRepository.GetAsync(purchaseOrderId);

        if (po.Status == Core.DocumentStatus.Draft || po.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", po.CompanyId);

        var invoice = new PurchaseInvoice(
            GuidGenerator.Create(),
            po.CompanyId,
            po.SupplierId,
            invoiceNumber,
            Clock.Now.Date,
            po.TenantId);

        invoice.CurrencyCode = po.CurrencyCode;
        invoice.Notes = po.Notes;

        // Deduct quantities already mapped in draft Purchase Invoices (per ERPNext PR #58617)
        var piQuery = await _purchaseInvoiceRepository.GetQueryableAsync();
        var draftInvoices = piQuery
            .Where(pi => pi.Status == Core.DocumentStatus.Draft)
            .SelectMany(pi => pi.Items)
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .GroupBy(i => i.PurchaseOrderItemId!.Value)
            .Select(g => new { PoItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToList();
        var draftInvoiceQtyByItem = draftInvoices.ToDictionary(x => x.PoItemId, x => x.Qty);

        foreach (var item in po.Items)
        {
            // Only bill pending qty minus draft mapped qty
            var draftQty = draftInvoiceQtyByItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = Math.Max(0, item.PendingBillingQty - draftQty);
            if (pendingQty > 0)
            {
                invoice.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom);
                var lastItem = invoice.Items.Last();
                lastItem.PurchaseOrderItemId = item.Id;
                lastItem.StockUom = item.StockUom;
                lastItem.ConversionFactor = item.ConversionFactor;
            }
        }

        if (invoice.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "PurchaseOrder")
                .WithData("documentNumber", po.OrderNumber)
                .WithData("reason", "All items in this Purchase Order have been fully billed or have pending draft invoices.");

        await _purchaseInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("PurchaseOrder", po.Id, po.CompanyId,
            "PurchaseInvoice", invoice.Id, po.OrderNumber, po.TenantId);

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<PurchaseInvoiceDto> ConvertPurchaseReceiptToInvoiceAsync(Guid purchaseReceiptId)
    {
        var receipt = await _purchaseReceiptRepository.GetAsync(purchaseReceiptId);

        if (receipt.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", receipt.CompanyId);

        var invoice = new PurchaseInvoice(
            GuidGenerator.Create(),
            receipt.CompanyId,
            receipt.SupplierId,
            invoiceNumber,
            Clock.Now.Date,
            receipt.TenantId);

        invoice.CurrencyCode = receipt.CurrencyCode;
        invoice.Notes = receipt.Notes;

        // Deduct quantities already mapped in draft Purchase Invoices (per ERPNext PR #58617)
        var piQuery2 = await _purchaseInvoiceRepository.GetQueryableAsync();
        var draftInvoices2 = piQuery2
            .Where(pi => pi.Status == Core.DocumentStatus.Draft)
            .SelectMany(pi => pi.Items)
            .Where(i => i.PurchaseReceiptItemId.HasValue)
            .GroupBy(i => i.PurchaseReceiptItemId!.Value)
            .Select(g => new { PrItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToList();
        var draftInvoiceQtyByPrItem = draftInvoices2.ToDictionary(x => x.PrItemId, x => x.Qty);

        foreach (var item in receipt.Items)
        {
            var draftQty = draftInvoiceQtyByPrItem.GetValueOrDefault(item.Id, 0m);
            var pendingQty = Math.Max(0, item.Quantity - item.BilledQty - draftQty);
            if (pendingQty <= 0) continue;

            invoice.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom);
            var lastItem = invoice.Items.Last();
            lastItem.PurchaseOrderItemId = item.PurchaseOrderItemId;
            lastItem.PurchaseReceiptItemId = item.Id;
            lastItem.StockUom = item.StockUom;
            lastItem.ConversionFactor = item.ConversionFactor;
        }

        if (invoice.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "PurchaseReceipt")
                .WithData("documentNumber", receipt.ReceiptNumber)
                .WithData("reason", "All items in this Purchase Receipt have been fully billed or have pending draft invoices.");

        await _purchaseInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("PurchaseReceipt", receipt.Id, receipt.CompanyId,
            "PurchaseInvoice", invoice.Id, receipt.ReceiptNumber, receipt.TenantId);

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
    }

    /// <summary>
    /// Converts a submitted Purchase Invoice into a Purchase Receipt.
    /// Per ERPNext PR #50971 / commit 66407d22fc: rejects return invoices and subtracts returned items (Debit Notes).
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<PurchaseReceiptDto> ConvertPurchaseInvoiceToReceiptAsync(Guid purchaseInvoiceId)
    {
        var pi = await _purchaseInvoiceRepository.GetAsync(purchaseInvoiceId);

        if (pi.Status == Core.DocumentStatus.Draft || pi.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (pi.IsReturn)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot create a Purchase Receipt for return Purchase Invoices (Debit Notes).");

        var receiptNumber = await _numberGenerator.GenerateAsync("PurchaseReceipt", pi.CompanyId);

        // Find debit notes against this purchase invoice to subtract returned quantities (PR #50971 / commit 66407d22fc)
        var piQuery = await _purchaseInvoiceRepository.GetQueryableAsync();
        var debitNotes = piQuery
            .Where(d => d.IsReturn && d.ReturnAgainstId == pi.Id && d.Status == Core.DocumentStatus.Submitted)
            .ToList();

        var returnedQtyByItem = debitNotes
            .SelectMany(dn => dn.Items)
            .GroupBy(dni => dni.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(dni => Math.Abs(dni.Quantity)));

        var warehouseId = pi.WarehouseId
            ?? pi.Items.FirstOrDefault(i => i.WarehouseId.HasValue)?.WarehouseId
            ?? (await _itemRepository.FindAsync(pi.Items.FirstOrDefault()?.ItemId ?? Guid.Empty))?.DefaultWarehouseId
            ?? throw new BusinessException("MyERP:01007")
                .WithData("documentType", "Purchase Receipt — no warehouse set on Purchase Invoice items");

        var receipt = new PurchaseReceipt(
            GuidGenerator.Create(),
            pi.CompanyId,
            pi.SupplierId,
            warehouseId,
            receiptNumber,
            Clock.Now.Date,
            pi.TenantId);

        receipt.CurrencyCode = pi.CurrencyCode;

        foreach (var item in pi.Items)
        {
            var returnedQty = returnedQtyByItem.GetValueOrDefault(item.ItemId, 0m);
            var pendingQty = item.Quantity - returnedQty;
            if (pendingQty <= 0) continue;

            receipt.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom);
            var lastItem = receipt.Items[^1];
            lastItem.StockUom = item.StockUom;
            lastItem.ConversionFactor = item.ConversionFactor;
            if (item.WarehouseId.HasValue)
                lastItem.WarehouseId = item.WarehouseId;
        }

        if (receipt.Items.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "PurchaseInvoice")
                .WithData("documentNumber", pi.InvoiceNumber)
                .WithData("reason", "All items have already been received or returned.");

        await _purchaseReceiptRepository.InsertAsync(receipt, autoSave: true);

        await _activityLog.LogConvertedAsync("PurchaseInvoice", pi.Id, pi.CompanyId,
            "PurchaseReceipt", receipt.Id, pi.InvoiceNumber, pi.TenantId);

        return ObjectMapper.Map<PurchaseReceipt, PurchaseReceiptDto>(receipt);
    }

    /// <summary>
    /// Converts a submitted Material Request (Purchase type) into a Purchase Order.
    /// MR items where OrderedQuantity &lt; Quantity get carried over.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<PurchaseOrderDto> ConvertMaterialRequestToPurchaseOrderAsync(
        Guid materialRequestId, Guid supplierId)
    {
        var mr = await _materialRequestRepository.GetAsync(materialRequestId);

        if (mr.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (mr.RequestType != MaterialRequestType.Purchase && mr.RequestType != MaterialRequestType.Subcontracting)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Only Purchase and Subcontracting Material Requests can be converted to PO");

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", mr.CompanyId);

        var po = new PurchaseOrder(
            GuidGenerator.Create(), mr.CompanyId, supplierId, orderNumber,
            Clock.Now.Date, mr.TenantId)
        {
            ExpectedDeliveryDate = mr.RequiredByDate ?? Clock.Now.Date,
            IsSubcontracted = mr.RequestType == MaterialRequestType.Subcontracting
        };

        // Account for draft Purchase Orders in the system (per ERPNext PR #58617 / commit d8432d92c8)
        var poQuery = await _purchaseOrderRepository.GetQueryableAsync();
        var draftPoItems = poQuery
            .Where(p => p.CompanyId == mr.CompanyId && p.Status == Core.DocumentStatus.Draft)
            .SelectMany(p => p.Items)
            .Where(i => i.MaterialRequestItemId.HasValue)
            .GroupBy(i => i.MaterialRequestItemId!.Value)
            .Select(g => new { MrItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToList();
        var draftPoQtyMap = draftPoItems.ToDictionary(x => x.MrItemId, x => x.Qty);

        foreach (var mrItem in mr.Items)
        {
            var draftQty = draftPoQtyMap.GetValueOrDefault(mrItem.Id, 0m);
            var pendingQty = Math.Max(0, MyERP.Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(mrItem) - draftQty);
            if (pendingQty <= 0) continue;

            // Get item for buying price
            var item = await _itemRepository.FindAsync(mrItem.ItemId);
            var rate = item?.StandardBuyingPrice ?? 0m;

            po.AddItem(mrItem.ItemId, mrItem.ItemName, pendingQty, rate, 0m, mrItem.Uom);

            // Link PO item back to MR item
            var poItem = po.Items.Last();
            poItem.MaterialRequestItemId = mrItem.Id;
            poItem.ConversionFactor = mrItem.ConversionFactor > 0 ? mrItem.ConversionFactor : 1m;
            poItem.StockUom = mrItem.Uom;
        }

        if (!po.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "MaterialRequest")
                .WithData("documentNumber", mr.RequestNumber)
                .WithData("reason", "All items in this Material Request have been fully ordered or have pending draft Purchase Orders.");

        await _purchaseOrderRepository.InsertAsync(po, autoSave: true);

        await _activityLog.LogConvertedAsync("MaterialRequest", mr.Id, mr.CompanyId,
            "PurchaseOrder", po.Id, mr.RequestNumber, mr.TenantId);

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    /// <summary>
    /// Creates Purchase Orders from MR with per-item supplier selection and qty adjustment.
    /// Per ERPNext PR #57676: creates one PO per supplier from selected items.
    /// Rejects duplicate MR items, validates qty against pending, groups by supplier.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SupplierSelectionResultDto> CreatePurchaseOrdersFromMaterialRequestAsync(
        CreatePurchaseOrdersFromMrDto input)
    {
        var mr = await _materialRequestRepository.GetAsync(input.MaterialRequestId);

        if (mr.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (mr.RequestType != MaterialRequestType.Purchase && mr.RequestType != MaterialRequestType.Subcontracting)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Only Purchase and Subcontracting Material Requests can be converted to PO");

        if (!input.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        // Per PR #57676: reject duplicate MR items in same selection
        var duplicateItemIds = input.Items
            .GroupBy(i => i.MaterialRequestItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateItemIds.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateMaterialRequestItemSelection)
                .WithData("reason", "Same Material Request item cannot be selected twice");

        // Validate each item's qty against pending
        foreach (var selItem in input.Items)
        {
            var mrItem = mr.Items.FirstOrDefault(i => i.Id == selItem.MaterialRequestItemId);
            if (mrItem == null)
                throw new BusinessException(MyERPDomainErrorCodes.MaterialRequestItemNotFound)
                    .WithData("reason", $"Material Request item not found");

            var pendingQty = MyERP.Purchasing.DomainServices.MaterialRequestManager.GetPendingQty(mrItem);
            if (selItem.Quantity > pendingQty)
                throw new BusinessException(MyERPDomainErrorCodes.QtyExceedsPendingMaterialRequest)
                    .WithData("reason", $"Requested qty ({selItem.Quantity}) exceeds pending qty ({pendingQty})");
        }

        // Group items by supplier → one PO per supplier
        var groupedBySupplier = input.Items.GroupBy(i => i.SupplierId);
        var result = new SupplierSelectionResultDto();

        foreach (var group in groupedBySupplier)
        {
            var supplierId = group.Key;
            var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", mr.CompanyId);

            var po = new PurchaseOrder(
                GuidGenerator.Create(), mr.CompanyId, supplierId, orderNumber,
                Clock.Now.Date, mr.TenantId)
            {
                ExpectedDeliveryDate = mr.RequiredByDate ?? Clock.Now.Date,
                IsSubcontracted = mr.RequestType == MaterialRequestType.Subcontracting
            };

            foreach (var selItem in group)
            {
                var mrItem = mr.Items.First(i => i.Id == selItem.MaterialRequestItemId);
                var item = await _itemRepository.FindAsync(mrItem.ItemId);
                var rate = item?.StandardBuyingPrice ?? 0m;

                po.AddItem(mrItem.ItemId, mrItem.ItemName, selItem.Quantity, rate, 0m, mrItem.Uom);

                var poItem = po.Items.Last();
                poItem.MaterialRequestItemId = mrItem.Id;
                poItem.ConversionFactor = mrItem.ConversionFactor > 0 ? mrItem.ConversionFactor : 1m;
                poItem.StockUom = mrItem.Uom;
            }

            await _purchaseOrderRepository.InsertAsync(po, autoSave: true);

            // Resolve supplier name for result
            var supplier = await _supplierRepository.FindAsync(supplierId);

            result.PurchaseOrders.Add(new CreatedPurchaseOrderInfo
            {
                PurchaseOrderId = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierName = supplier?.Name,
                ItemCount = po.Items.Count,
                TotalAmount = po.GrandTotal
            });
            result.TotalItemsOrdered += po.Items.Count;
        }

        await _activityLog.LogConvertedAsync("MaterialRequest", mr.Id, mr.CompanyId,
            "PurchaseOrder", result.PurchaseOrders.First().PurchaseOrderId, mr.RequestNumber, mr.TenantId);

        return result;
    }

    /// <summary>
    /// Creates Supplier Quotations from a submitted RFQ — one SQ per RFQ supplier.
    /// Per ERPNext: RFQ detail has "Make Supplier Quotation" per supplier.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SupplierQuotationDto> ConvertRfqToSupplierQuotationAsync(
        Guid rfqId, Guid supplierId)
    {
        var rfq = await _rfqRepository.GetAsync(rfqId);

        if (rfq.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        // Validate that the supplier is on the RFQ
        var rfqSupplier = rfq.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId);
        if (rfqSupplier == null)
            throw new BusinessException("MyERP:04016")
                .WithData("reason", "Supplier is not listed on this RFQ");

        // Check if SQ already exists for this RFQ + supplier
        var existingQuery = await _sqRepository.GetQueryableAsync();
        var alreadyExists = existingQuery.Any(sq =>
            sq.RequestForQuotationId == rfqId &&
            sq.SupplierId == supplierId &&
            sq.Status != Core.DocumentStatus.Cancelled);
        if (alreadyExists)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("reason", "Supplier Quotation already exists for this supplier on this RFQ");

        var sqNumber = await _numberGenerator.GenerateAsync("SupplierQuotation", rfq.CompanyId);

        var sq = new SupplierQuotation(
            GuidGenerator.Create(),
            rfq.CompanyId,
            supplierId,
            Clock.Now.Date,
            rfq.TenantId);

        sq.QuotationNumber = sqNumber;
        sq.RequestForQuotationId = rfq.Id;
        sq.Currency = rfq.CurrencyCode;

        var allowZeroQty = await SettingProvider.IsTrueAsync(MyERP.Settings.MyERPSettings.Buying.AllowZeroQtyInSupplierQuotation);
        foreach (var rfqItem in rfq.Items)
        {
            if (rfqItem.Qty <= 0 && !allowZeroQty)
                continue;

            // Rate starts at 0 — supplier fills in their quoted rate
            sq.AddItem(rfqItem.ItemId, rfqItem.Qty, 0m, rfqItem.Description,
                rfqItem.Uom);
        }

        await _sqRepository.InsertAsync(sq, autoSave: true);

        // Mark RFQ supplier as quote received (will be updated when SQ is submitted)
        rfqSupplier.EmailSent = true;
        await _rfqRepository.UpdateAsync(rfq, autoSave: true);

        await _activityLog.LogConvertedAsync("RequestForQuotation", rfq.Id, rfq.CompanyId,
            "SupplierQuotation", sq.Id, rfq.RfqNumber, rfq.TenantId);

        return ObjectMapper.Map<SupplierQuotation, SupplierQuotationDto>(sq);
    }

    /// <summary>
    /// Converts a Supplier Quotation to a Purchase Order.
    /// Per ERPNext PR #58572: copies items with remaining pending order qty to a new PO.
    /// SQ must be Submitted or Partially Ordered. Creates Draft PO for review before submission.
    /// This completes the procurement cycle: MR → RFQ → SQ → PO → PR → PI.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<PurchaseOrderDto> ConvertSupplierQuotationToPurchaseOrderAsync(Guid supplierQuotationId)
    {
        var sq = await _sqRepository.GetAsync(supplierQuotationId);

        if (sq.Status != Core.DocumentStatus.Submitted && sq.Status != Core.DocumentStatus.ToDeliverAndBill)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (!sq.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.SupplierQuotationHasNoItems)
                .WithData("reason", "Supplier Quotation has no items to convert");

        // Filter items with pending order quantity
        var pendingItems = sq.Items.Where(i => i.PendingOrderQty > 0 || (i.StockQty == 0 && i.OrderedQty == 0)).ToList();
        if (!pendingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "SupplierQuotation")
                .WithData("documentNumber", sq.QuotationNumber ?? "")
                .WithData("reason", "All items in this Supplier Quotation have already been fully ordered.");
        }

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", sq.CompanyId);

        var po = new PurchaseOrder(
            GuidGenerator.Create(),
            sq.CompanyId,
            sq.SupplierId,
            orderNumber,
            Clock.Now.Date,
            sq.TenantId);

        po.SupplierQuotationId = supplierQuotationId;
        po.CurrencyCode = sq.Currency;
        po.ExchangeRate = sq.ExchangeRate;

        foreach (var sqItem in pendingItems)
        {
            var convertQty = sqItem.ConversionFactor > 0
                ? sqItem.PendingOrderQty / sqItem.ConversionFactor
                : sqItem.Qty;

            if (convertQty <= 0 && sqItem.Qty > 0) continue;

            po.AddItem(sqItem.ItemId, sqItem.ItemName ?? "", convertQty > 0 ? convertQty : sqItem.Qty, sqItem.Rate, 0m, sqItem.Uom ?? "Unit");
            var lastPoItem = po.Items.Last();
            lastPoItem.SupplierQuotationItemId = sqItem.Id;
            lastPoItem.StockUom = sqItem.StockUom;
            lastPoItem.ConversionFactor = sqItem.ConversionFactor;
        }

        await _purchaseOrderRepository.InsertAsync(po, autoSave: true);

        await _activityLog.LogConvertedAsync("SupplierQuotation", sq.Id, sq.CompanyId,
            "PurchaseOrder", po.Id, sq.QuotationNumber, sq.TenantId);

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }

    /// <summary>
    /// Converts a submitted Material Request into a Request for Quotation.
    /// Per ERPNext PR #58534 (commit c93815b4ae): filters out fully ordered and received items.
    /// Qty is set to remaining stock qty divided by conversion factor.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<RfqDto> ConvertMaterialRequestToRfqAsync(Guid materialRequestId)
    {
        var mr = await _materialRequestRepository.GetAsync(materialRequestId);

        if (mr.Status == Core.DocumentStatus.Draft || mr.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (mr.RequestType != MaterialRequestType.Purchase)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Only Purchase Material Requests can be converted to Request for Quotation");
        }

        if (!mr.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        // Per ERPNext commit c93815b4ae: filter out items where ordered_qty or received_qty covers stock_qty
        var pendingItems = mr.Items
            .Where(i => Math.Max(i.OrderedQuantity, i.ReceivedQuantity) < i.StockQty)
            .ToList();

        if (!pendingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted)
                .WithData("documentType", "MaterialRequest")
                .WithData("documentNumber", mr.RequestNumber)
                .WithData("reason", "All items in this Material Request have already been fully ordered or received.");
        }

        var rfqNumber = await _numberGenerator.GenerateAsync("RequestForQuotation", mr.CompanyId);

        var rfq = new RequestForQuotation(
            GuidGenerator.Create(),
            mr.CompanyId,
            rfqNumber,
            Clock.Now.Date,
            mr.TenantId);

        foreach (var mrItem in pendingItems)
        {
            var fulfilledQty = Math.Max(mrItem.OrderedQuantity, mrItem.ReceivedQuantity);
            var remainingStockQty = mrItem.StockQty - fulfilledQty;
            var remainingQty = mrItem.ConversionFactor > 0
                ? remainingStockQty / mrItem.ConversionFactor
                : remainingStockQty;

            if (remainingQty <= 0) continue;

            rfq.AddItem(
                mrItem.ItemId,
                mrItem.ItemName,
                remainingQty,
                mrItem.Uom,
                mrItem.WarehouseId,
                mrItem.Id);
        }

        if (!rfq.Items.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems)
                .WithData("reason", "No valid items remaining to include in Request for Quotation.");
        }

        await _rfqRepository.InsertAsync(rfq, autoSave: true);

        await _activityLog.LogConvertedAsync("MaterialRequest", mr.Id, mr.CompanyId,
            "RequestForQuotation", rfq.Id, mr.RequestNumber, mr.TenantId);

        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }
}
