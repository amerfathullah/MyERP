using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
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
/// Deducts stock from the specified warehouse.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Create)]
public class PosAppService : ApplicationService, IPosAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockValuationService _stockValuationService;
    private readonly BinService _binService;

    public PosAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Item, Guid> itemRepository,
        IDocumentNumberGenerator numberGenerator,
        StockValuationService stockValuationService,
        BinService binService)
    {
        _invoiceRepository = invoiceRepository;
        _itemRepository = itemRepository;
        _numberGenerator = numberGenerator;
        _stockValuationService = stockValuationService;
        _binService = binService;
    }

    public async Task<PosInvoiceDto> CompleteSaleAsync(CreatePosInvoiceDto input)
    {
        // Validate posting period is not frozen/closed
        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(input.CompanyId, DateTime.UtcNow, "POS Invoice");

        var invoiceNumber = await _numberGenerator.GenerateAsync("POS", input.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId ?? Guid.Empty, // Walk-in customer
            invoiceNumber,
            DateTime.UtcNow,
            CurrentTenant.Id);

        // POS always deducts stock
        invoice.UpdateStock = true;
        invoice.WarehouseId = input.WarehouseId;

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount);
        }

        // POS invoices go straight to Posted status
        invoice.Submit();
        invoice.Post();

        // Deduct stock for stock items
        if (input.WarehouseId.HasValue)
        {
            foreach (var item in input.Items)
            {
                var stockItem = await _itemRepository.FindAsync(item.ItemId);
                if (stockItem?.MaintainStock != true) continue;

                await _stockValuationService.CreateLedgerEntryAsync(
                    input.CompanyId, item.ItemId, input.WarehouseId.Value,
                    DateTime.UtcNow, -item.Quantity, item.UnitPrice,
                    "SalesInvoice", invoice.Id, CurrentTenant.Id);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, input.WarehouseId.Value, -item.Quantity, -(item.Quantity * item.UnitPrice));
            }
        }

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
