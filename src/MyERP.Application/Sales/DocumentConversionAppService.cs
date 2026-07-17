using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
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

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<SalesOrderDto> ConvertQuotationToSalesOrderAsync(Guid quotationId)
    {
        var quotation = await _quotationRepository.GetAsync(quotationId);

        if (quotation.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (quotation.ConvertedToSalesOrderId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted);

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

        foreach (var item in quotation.Items)
        {
            salesOrder.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        quotation.ConvertedToSalesOrderId = salesOrder.Id;

        await _salesOrderRepository.InsertAsync(salesOrder, autoSave: true);
        await _quotationRepository.UpdateAsync(quotation, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("Quotation", quotation.Id, quotation.CompanyId,
            "SalesOrder", salesOrder.Id, quotation.QuotationNumber, quotation.TenantId);

        var soDto = ObjectMapper.Map<SalesOrder, SalesOrderDto>(salesOrder);
        try { soDto.CustomerName = (await _customerRepository.GetAsync(salesOrder.CustomerId)).Name; } catch { }
        return soDto;
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteAsync(Guid salesOrderId)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);

        if (salesOrder.Status == Core.DocumentStatus.Draft || salesOrder.Status == Core.DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        var deliveryNumber = await _numberGenerator.GenerateAsync("DeliveryNote", salesOrder.CompanyId);

        var deliveryNote = new DeliveryNote(
            GuidGenerator.Create(),
            salesOrder.CompanyId,
            salesOrder.CustomerId,
            Guid.Empty, // Warehouse to be set by user on the draft
            deliveryNumber,
            Clock.Now.Date,
            salesOrder.TenantId);

        deliveryNote.SalesOrderId = salesOrder.Id;
        deliveryNote.CurrencyCode = salesOrder.CurrencyCode;

        foreach (var item in salesOrder.Items)
        {
            // Only convert pending delivery qty (skip already-delivered items)
            var pendingQty = item.PendingDeliveryQty;
            if (pendingQty > 0)
            {
                deliveryNote.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom, item.Id);
            }
        }

        await _deliveryNoteRepository.InsertAsync(deliveryNote, autoSave: true);

        // Audit trail
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

        foreach (var item in salesOrder.Items)
        {
            // Only bill pending qty
            var pendingQty = item.PendingBillingQty;
            if (pendingQty > 0)
            {
                invoice.AddItem(item.ItemId, item.Description, pendingQty, item.UnitPrice, item.TaxAmount, item.Uom);
                // Set the SO item link for billing tracking
                var lastItem = invoice.Items.Last();
                lastItem.SalesOrderItemId = item.Id;
            }
        }

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("SalesOrder", salesOrder.Id, salesOrder.CompanyId,
            "SalesInvoice", invoice.Id, salesOrder.OrderNumber, salesOrder.TenantId);

        var siDto1 = ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
        try { siDto1.CustomerName = (await _customerRepository.GetAsync(invoice.CustomerId)).Name; } catch { }
        return siDto1;
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> ConvertDeliveryNoteToSalesInvoiceAsync(Guid deliveryNoteId)
    {
        var deliveryNote = await _deliveryNoteRepository.GetAsync(deliveryNoteId);

        if (deliveryNote.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

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
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
            // Carry through SO item link from DN item
            var lastItem = invoice.Items.Last();
            lastItem.SalesOrderItemId = item.SalesOrderItemId;
        }

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        // Audit trail
        await _activityLog.LogConvertedAsync("DeliveryNote", deliveryNote.Id, deliveryNote.CompanyId,
            "SalesInvoice", invoice.Id, deliveryNote.DeliveryNumber, deliveryNote.TenantId);

        var siDto2 = ObjectMapper.Map<SalesInvoice, SalesInvoiceDto>(invoice);
        try { siDto2.CustomerName = (await _customerRepository.GetAsync(invoice.CustomerId)).Name; } catch { }
        return siDto2;
    }
}
