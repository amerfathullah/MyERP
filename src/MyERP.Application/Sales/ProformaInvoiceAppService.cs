using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Application.Contracts.Sales;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Application.Sales;

/// <summary>
/// Proforma Invoice AppService — progressive/partial invoicing before delivery (v16 feature).
/// Per ERPNext PR #57263. Gated by Selling Settings.EnableProformaInvoice.
/// </summary>
[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class ProformaInvoiceAppService : ApplicationService
{
    private readonly IRepository<ProformaInvoice, Guid> _repository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ProformaInvoiceAppService(
        IRepository<ProformaInvoice, Guid> repository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<ProformaInvoiceDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return await MapToDtoAsync(entity);
    }

    public async Task<PagedResultDto<ProformaInvoiceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(x => x.ProformaDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = new List<ProformaInvoiceDto>();
        foreach (var item in items)
            dtos.Add(await MapToDtoAsync(item));

        return new PagedResultDto<ProformaInvoiceDto>(totalCount, dtos);
    }

    /// <summary>
    /// Get all proforma invoices for a Sales Order (for the SO detail's Proforma tab).
    /// </summary>
    public async Task<List<ProformaInvoiceDto>> GetForSalesOrderAsync(Guid salesOrderId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var items = queryable
            .Where(x => x.SalesOrderId == salesOrderId)
            .OrderByDescending(x => x.ProformaDate)
            .ToList();

        var dtos = new List<ProformaInvoiceDto>();
        foreach (var item in items)
            dtos.Add(await MapToDtoAsync(item));

        return dtos;
    }

    /// <summary>
    /// Get proformed totals per SO item (for the creation dialog showing remaining qty/amount).
    /// </summary>
    public async Task<List<ProformedTotalsDto>> GetProformedTotalsAsync(Guid salesOrderId)
    {
        var so = await _salesOrderRepository.GetAsync(salesOrderId);
        var queryable = await _repository.GetQueryableAsync();
        var issuedProformas = queryable
            .Where(x => x.SalesOrderId == salesOrderId && x.Status == ProformaInvoiceStatus.Issued)
            .ToList();

        var proformedBySoItem = issuedProformas
            .SelectMany(p => p.Items)
            .GroupBy(i => i.SalesOrderItemId)
            .ToDictionary(
                g => g.Key,
                g => (Qty: g.Sum(x => x.Quantity), Amount: g.Sum(x => x.Amount)));

        return so.Items.Select(soItem =>
        {
            var proformed = proformedBySoItem.GetValueOrDefault(soItem.Id);
            return new ProformedTotalsDto
            {
                SalesOrderItemId = soItem.Id,
                ItemCode = soItem.Description ?? string.Empty,
                ItemName = soItem.Description ?? string.Empty,
                OrderedQty = soItem.Quantity,
                OrderedAmount = soItem.Quantity * soItem.UnitPrice,
                ProformedQty = proformed.Qty,
                ProformedAmount = proformed.Amount,
                RemainingQty = Math.Max(0, soItem.Quantity - proformed.Qty),
                RemainingAmount = Math.Max(0, (soItem.Quantity * soItem.UnitPrice) - proformed.Amount)
            };
        }).ToList();
    }

    /// <summary>
    /// Create and submit a Proforma Invoice against a submitted Sales Order.
    /// Per ERPNext: the sole creation path (in_create = true, no direct form creation).
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<ProformaInvoiceDto> CreateAsync(CreateProformaInvoiceDto input)
    {
        var so = await _salesOrderRepository.GetAsync(input.SalesOrderId);

        // Must be submitted (any active fulfillment status)
        if (so.Status == DocumentStatus.Draft || so.Status == DocumentStatus.Cancelled)
            throw new BusinessException("MyERP:07001")
                .WithData("documentType", "SalesOrder");

        var proformaNumber = await _numberGenerator.GenerateAsync("PRO", so.CompanyId);
        var proforma = new ProformaInvoice(
            GuidGenerator.Create(),
            so.CompanyId,
            so.Id,
            so.CustomerId,
            DateTime.UtcNow.Date,
            input.BasedOn,
            so.CurrencyCode);

        proforma.ProformaNumber = proformaNumber;
        proforma.HideItemQty = input.BasedOn == ProformaInvoiceBasis.Amount && input.HideItemQty;

        // Build SO item lookup
        var soItems = so.Items.ToDictionary(i => i.Id);

        foreach (var row in input.Items)
        {
            if (!soItems.TryGetValue(row.SalesOrderItemId, out var soItem))
                continue;

            decimal qty = row.Quantity;
            decimal rate;

            if (input.BasedOn == ProformaInvoiceBasis.Amount)
            {
                // Amount basis: both qty and amount are user-entered, rate = amount / qty
                var amount = row.Amount ?? 0;
                if (amount <= 0 || qty <= 0) continue;
                rate = Math.Round(amount / qty, 4);
            }
            else
            {
                // Quantity basis: rate from SO, amount = qty × rate
                if (qty <= 0) continue;
                rate = soItem.UnitPrice;
            }

            proforma.AddItem(
                soItem.Id,
                soItem.ItemId,
                soItem.Description ?? string.Empty,
                soItem.Description ?? string.Empty,
                qty,
                rate,
                soItem.StockUom);
        }

        if (!proforma.Items.Any())
            throw new BusinessException("MyERP:01007");

        // Auto-submit on creation (per ERPNext: proforma is insert+submit in one operation)
        proforma.Submit();

        await _repository.InsertAsync(proforma);
        return await MapToDtoAsync(proforma);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task CancelAsync(Guid id)
    {
        var proforma = await _repository.GetAsync(id);
        proforma.Cancel();
        await _repository.UpdateAsync(proforma);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Default)]
    public async Task SendEmailAsync(Guid id, SendProformaEmailDto input)
    {
        var proforma = await _repository.GetAsync(id);
        proforma.MarkEmailed(input.Recipients);
        await _repository.UpdateAsync(proforma);
        // Note: actual email sending would be done via ABP email module
        // This records the intent + timestamps
    }

    private async Task<ProformaInvoiceDto> MapToDtoAsync(ProformaInvoice entity)
    {
        string? customerName = null;
        string? soNumber = null;

        var customer = await _customerRepository.FindAsync(entity.CustomerId);
        if (customer != null) customerName = customer.Name;

        var so = await _salesOrderRepository.FindAsync(entity.SalesOrderId);
        if (so != null) soNumber = so.OrderNumber;

        return new ProformaInvoiceDto
        {
            Id = entity.Id,
            ProformaNumber = entity.ProformaNumber,
            ProformaDate = entity.ProformaDate,
            SalesOrderId = entity.SalesOrderId,
            SalesOrderNumber = soNumber,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            BasedOn = entity.BasedOn,
            HideItemQty = entity.HideItemQty,
            CurrencyCode = entity.CurrencyCode,
            GrandTotal = entity.GrandTotal,
            TotalQty = entity.TotalQty,
            Status = entity.Status,
            ProformaPdfUrl = entity.ProformaPdfUrl,
            SentOn = entity.SentOn,
            EmailedTo = entity.EmailedTo,
            Items = entity.Items.Select(i => new ProformaInvoiceItemDto
            {
                Id = i.Id,
                SalesOrderItemId = i.SalesOrderItemId,
                ItemId = i.ItemId,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                Uom = i.Uom,
                Quantity = i.Quantity,
                Rate = i.Rate,
                Amount = i.Amount
            }).ToList()
        };
    }
}
