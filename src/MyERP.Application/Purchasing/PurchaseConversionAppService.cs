using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
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
[Authorize]
public class PurchaseConversionAppService : ApplicationService, IPurchaseConversionAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _purchaseReceiptRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PurchaseConversionAppService(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<PurchaseReceipt, Guid> purchaseReceiptRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _numberGenerator = numberGenerator;
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<PurchaseReceiptDto> ConvertPurchaseOrderToReceiptAsync(Guid purchaseOrderId)
    {
        var po = await _purchaseOrderRepository.GetAsync(purchaseOrderId);

        if (po.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var receiptNumber = await _numberGenerator.GenerateAsync("PurchaseReceipt", po.CompanyId);

        var receipt = new PurchaseReceipt(
            GuidGenerator.Create(),
            po.CompanyId,
            po.SupplierId,
            Guid.Empty, // Warehouse to be set by user after creation
            receiptNumber,
            Clock.Now.Date,
            po.TenantId);

        receipt.PurchaseOrderId = po.Id;
        receipt.CurrencyCode = po.CurrencyCode;

        foreach (var item in po.Items)
        {
            receipt.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _purchaseReceiptRepository.InsertAsync(receipt, autoSave: true);

        return MapReceiptToDto(receipt);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<PurchaseInvoiceDto> ConvertPurchaseOrderToInvoiceAsync(Guid purchaseOrderId)
    {
        var po = await _purchaseOrderRepository.GetAsync(purchaseOrderId);

        if (po.Status != Core.DocumentStatus.Submitted)
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
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _purchaseInvoiceRepository.InsertAsync(invoice, autoSave: true);

        return MapInvoiceToDto(invoice);
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
        }

        await _purchaseInvoiceRepository.InsertAsync(invoice, autoSave: true);

        return MapInvoiceToDto(invoice);
    }

    private PurchaseReceiptDto MapReceiptToDto(PurchaseReceipt receipt)
    {
        return new PurchaseReceiptDto
        {
            Id = receipt.Id,
            CompanyId = receipt.CompanyId,
            ReceiptNumber = receipt.ReceiptNumber,
            PostingDate = receipt.PostingDate,
            SupplierId = receipt.SupplierId,
            PurchaseOrderId = receipt.PurchaseOrderId,
            WarehouseId = receipt.WarehouseId,
            SupplierDeliveryNote = receipt.SupplierDeliveryNote,
            CurrencyCode = receipt.CurrencyCode,
            NetTotal = receipt.NetTotal,
            TaxAmount = receipt.TaxAmount,
            GrandTotal = receipt.GrandTotal,
            IsReturn = receipt.IsReturn,
            Status = receipt.Status.ToString(),
            Items = receipt.Items.Select(i => new PurchaseReceiptItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                Description = i.Description,
                Uom = i.Uom,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal,
            }).ToList(),
        };
    }

    private PurchaseInvoiceDto MapInvoiceToDto(PurchaseInvoice invoice)
    {
        return new PurchaseInvoiceDto
        {
            Id = invoice.Id,
            CompanyId = invoice.CompanyId,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            SupplierId = invoice.SupplierId,
            SupplierTin = invoice.SupplierTin,
            CurrencyCode = invoice.CurrencyCode,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            AmountPaid = invoice.AmountPaid,
            OutstandingAmount = invoice.OutstandingAmount,
            Status = invoice.Status.ToString(),
            EInvoiceStatus = invoice.EInvoiceStatus.ToString(),
            LhdnUuid = invoice.LhdnUuid,
            Items = invoice.Items.Select(i => new PurchaseInvoiceItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                Description = i.Description,
                Uom = i.Uom,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxAmount = i.TaxAmount,
                LineTotal = i.LineTotal,
            }).ToList(),
        };
    }
}
