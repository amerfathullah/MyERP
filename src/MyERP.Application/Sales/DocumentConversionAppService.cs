using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
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

    public DocumentConversionAppService(
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<SalesOrderDto> ConvertQuotationToSalesOrderAsync(Guid quotationId)
    {
        var quotation = await _quotationRepository.GetAsync(quotationId);

        if (quotation.Status != Core.DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustBeSubmittedForConversion);

        if (quotation.ConvertedToSalesOrderId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.DocumentAlreadyConverted);

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

        return await MapSalesOrderToDtoAsync(salesOrder);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteAsync(Guid salesOrderId)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);

        if (salesOrder.Status != Core.DocumentStatus.Submitted)
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
            deliveryNote.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _deliveryNoteRepository.InsertAsync(deliveryNote, autoSave: true);

        return MapDeliveryNoteToDto(deliveryNote);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> ConvertSalesOrderToSalesInvoiceAsync(Guid salesOrderId)
    {
        var salesOrder = await _salesOrderRepository.GetAsync(salesOrderId);

        if (salesOrder.Status != Core.DocumentStatus.Submitted)
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
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        return await MapSalesInvoiceToDtoAsync(invoice);
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
        }

        await _salesInvoiceRepository.InsertAsync(invoice, autoSave: true);

        return await MapSalesInvoiceToDtoAsync(invoice);
    }

    private async Task<SalesOrderDto> MapSalesOrderToDtoAsync(SalesOrder order)
    {
        string? customerName = null;
        try { customerName = (await _customerRepository.GetAsync(order.CustomerId)).Name; } catch { }

        return new SalesOrderDto
        {
            Id = order.Id,
            CompanyId = order.CompanyId,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            CustomerId = order.CustomerId,
            CustomerName = customerName,
            CustomerPoNumber = order.CustomerPoNumber,
            CurrencyCode = order.CurrencyCode,
            NetTotal = order.NetTotal,
            TaxAmount = order.TaxAmount,
            GrandTotal = order.GrandTotal,
            Terms = order.Terms,
            Notes = order.Notes,
            Status = order.Status.ToString(),
            QuotationId = order.QuotationId,
            Items = order.Items.Select(i => new SalesOrderItemDto
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

    private DeliveryNoteDto MapDeliveryNoteToDto(DeliveryNote dn)
    {
        return new DeliveryNoteDto
        {
            Id = dn.Id,
            CompanyId = dn.CompanyId,
            DeliveryNumber = dn.DeliveryNumber,
            PostingDate = dn.PostingDate,
            CustomerId = dn.CustomerId,
            SalesOrderId = dn.SalesOrderId,
            WarehouseId = dn.WarehouseId,
            CurrencyCode = dn.CurrencyCode,
            NetTotal = dn.NetTotal,
            TaxAmount = dn.TaxAmount,
            GrandTotal = dn.GrandTotal,
            IsReturn = dn.IsReturn,
            Status = dn.Status.ToString(),
            Items = dn.Items.Select(i => new DeliveryNoteItemDto
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

    private async Task<SalesInvoiceDto> MapSalesInvoiceToDtoAsync(SalesInvoice invoice)
    {
        string? customerName = null;
        try { customerName = (await _customerRepository.GetAsync(invoice.CustomerId)).Name; } catch { }

        return new SalesInvoiceDto
        {
            Id = invoice.Id,
            CompanyId = invoice.CompanyId,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CustomerId = invoice.CustomerId,
            CustomerName = customerName,
            CurrencyCode = invoice.CurrencyCode,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            AmountPaid = invoice.AmountPaid,
            OutstandingAmount = invoice.OutstandingAmount,
            Status = invoice.Status.ToString(),
            EInvoiceStatus = invoice.EInvoiceStatus.ToString(),
            LhdnUuid = invoice.LhdnUuid,
            Items = invoice.Items.Select(i => new SalesInvoiceItemDto
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
