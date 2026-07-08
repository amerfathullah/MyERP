using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class PurchaseInvoiceAppService : ApplicationService, IPurchaseInvoiceAppService
{
    private readonly IRepository<PurchaseInvoice, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PurchaseInvoiceAppService(
        IRepository<PurchaseInvoice, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PurchaseInvoiceDto> GetAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        return MapToDto(invoice);
    }

    public async Task<PagedResultDto<PurchaseInvoiceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var invoices = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "IssueDate DESC");

        return new PagedResultDto<PurchaseInvoiceDto>(
            totalCount,
            invoices.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<PurchaseInvoiceDto> CreateAsync(CreatePurchaseInvoiceDto input)
    {
        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", input.CompanyId);

        var invoice = new PurchaseInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.SupplierId,
            invoiceNumber,
            input.IssueDate);

        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;
        invoice.SupplierInvoiceNumber = input.SupplierInvoiceNumber;
        invoice.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Submit)]
    public async Task<PurchaseInvoiceDto> SubmitAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Submit();
        await _repository.UpdateAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Submit)]
    public async Task<PurchaseInvoiceDto> PostAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Post();
        await _repository.UpdateAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.PurchaseInvoices.Cancel)]
    public async Task<PurchaseInvoiceDto> CancelAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Cancel();
        await _repository.UpdateAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    private static PurchaseInvoiceDto MapToDto(PurchaseInvoice invoice) => new()
    {
        Id = invoice.Id,
        CompanyId = invoice.CompanyId,
        InvoiceNumber = invoice.InvoiceNumber,
        SupplierInvoiceNumber = invoice.SupplierInvoiceNumber,
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
            LineTotal = i.LineTotal
        }).ToList()
    };
}
