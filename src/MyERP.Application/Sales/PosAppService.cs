using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Point of Sale application service.
/// Creates a Sales Invoice in Posted status directly (like ERPNext POS Invoice).
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Create)]
public class PosAppService : ApplicationService, IPosAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PosAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Item, Guid> itemRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _invoiceRepository = invoiceRepository;
        _itemRepository = itemRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PosInvoiceDto> CompleteSaleAsync(CreatePosInvoiceDto input)
    {
        var invoiceNumber = await _numberGenerator.GenerateAsync("POS", input.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId ?? Guid.Empty, // Walk-in customer
            invoiceNumber,
            DateTime.UtcNow,
            CurrentTenant.Id);

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount);
        }

        // POS invoices go straight to Posted status
        invoice.Submit();
        invoice.Post();

        await _invoiceRepository.InsertAsync(invoice, autoSave: true);

        var change = input.AmountReceived - invoice.GrandTotal;

        return new PosInvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            AmountReceived = input.AmountReceived,
            Change = change > 0 ? change : 0,
            Status = invoice.Status.ToString(),
        };
    }

    public async Task<PagedResultDto<PosItemDto>> SearchItemsAsync(PosItemSearchDto input)
    {
        var query = await _itemRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            query = query.Where(i => i.IsActive &&
                (i.ItemName.ToLower().Contains(search) ||
                 i.ItemCode.ToLower().Contains(search) ||
                 (i.Barcode != null && i.Barcode.ToLower().Contains(search))));
        }
        else
        {
            query = query.Where(i => i.IsActive);
        }

        var items = query.Take(input.MaxResultCount).ToList();

        return new PagedResultDto<PosItemDto>(
            items.Count,
            items.Select(i => new PosItemDto
            {
                Id = i.Id,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                SellingPrice = i.StandardSellingPrice ?? 0,
                Uom = i.Uom,
                Barcode = i.Barcode,
            }).ToList());
    }
}
