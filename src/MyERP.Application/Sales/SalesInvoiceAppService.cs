using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesInvoiceAppService : ApplicationService, ISalesInvoiceAppService
{
    private readonly IRepository<SalesInvoice, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public SalesInvoiceAppService(
        IRepository<SalesInvoice, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<SalesInvoiceDto> GetAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        return MapToDto(invoice);
    }

    public async Task<PagedResultDto<SalesInvoiceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var invoices = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "IssueDate DESC");

        return new PagedResultDto<SalesInvoiceDto>(
            totalCount,
            invoices.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<SalesInvoiceDto> CreateAsync(CreateSalesInvoiceDto input)
    {
        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", input.CompanyId);

        var invoice = new SalesInvoice(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            invoiceNumber,
            input.IssueDate);

        invoice.DueDate = input.DueDate;
        invoice.CurrencyCode = input.CurrencyCode;

        foreach (var item in input.Items)
        {
            invoice.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> SubmitAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Submit();
        await _repository.UpdateAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Submit)]
    public async Task<SalesInvoiceDto> PostAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Post();
        await _repository.UpdateAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    [Authorize(MyERPPermissions.SalesInvoices.Cancel)]
    public async Task<SalesInvoiceDto> CancelAsync(Guid id)
    {
        var invoice = await _repository.GetAsync(id);
        invoice.Cancel();
        await _repository.UpdateAsync(invoice, autoSave: true);
        return MapToDto(invoice);
    }

    private SalesInvoiceDto MapToDto(SalesInvoice invoice)
    {
        return new SalesInvoiceDto
        {
            Id = invoice.Id,
            CompanyId = invoice.CompanyId,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CustomerId = invoice.CustomerId,
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
                LineTotal = i.LineTotal
            }).ToList()
        };
    }
}
