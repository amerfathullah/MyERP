using System;
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
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentActivityLogService _activityLog;

    public PurchaseConversionAppService(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<Item, Guid> itemRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentActivityLogService activityLog)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _materialRequestRepository = materialRequestRepository;
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

        foreach (var item in po.Items)
        {
            // Only convert pending receipt qty (supports partial receipts)
            var pendingQty = item.PendingReceiptQty;
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

        foreach (var item in po.Items)
        {
            // Only bill pending qty
            var pendingQty = item.PendingBillingQty;
            if (pendingQty > 0)
            {
                invoice.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom);
                var lastItem = invoice.Items.Last();
                lastItem.PurchaseOrderItemId = item.Id;
                lastItem.StockUom = item.StockUom;
                lastItem.ConversionFactor = item.ConversionFactor;
            }
        }

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

        foreach (var item in receipt.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            var lastItem = invoice.Items.Last();
            lastItem.PurchaseOrderItemId = item.PurchaseOrderItemId;
            lastItem.PurchaseReceiptItemId = item.Id;
            lastItem.StockUom = item.StockUom;
            lastItem.ConversionFactor = item.ConversionFactor;
        }

        await _purchaseInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("PurchaseReceipt", receipt.Id, receipt.CompanyId,
            "PurchaseInvoice", invoice.Id, receipt.ReceiptNumber, receipt.TenantId);

        return ObjectMapper.Map<PurchaseInvoice, PurchaseInvoiceDto>(invoice);
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

        if (mr.RequestType != MaterialRequestType.Purchase)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Only Purchase-type Material Requests can be converted to PO");

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", mr.CompanyId);

        var po = new PurchaseOrder(
            GuidGenerator.Create(), mr.CompanyId, supplierId, orderNumber,
            Clock.Now.Date, mr.TenantId);

        foreach (var mrItem in mr.Items)
        {
            var pendingQty = mrItem.Quantity - mrItem.OrderedQuantity;
            if (pendingQty <= 0) continue;

            // Get item for buying price
            var item = await _itemRepository.FindAsync(mrItem.ItemId);
            var rate = item?.StandardBuyingPrice ?? 0m;

            po.AddItem(mrItem.ItemId, mrItem.ItemName, pendingQty, rate, 0m, mrItem.Uom);

            // Link PO item back to MR item
            var poItem = po.Items.Last();
            poItem.MaterialRequestItemId = mrItem.Id;
        }

        if (!po.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted);

        await _purchaseOrderRepository.InsertAsync(po, autoSave: true);

        await _activityLog.LogConvertedAsync("MaterialRequest", mr.Id, mr.CompanyId,
            "PurchaseOrder", po.Id, mr.RequestNumber, mr.TenantId);

        return ObjectMapper.Map<PurchaseOrder, PurchaseOrderDto>(po);
    }
}
