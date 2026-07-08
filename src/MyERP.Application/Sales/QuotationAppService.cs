using System;
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

[Authorize(MyERPPermissions.Quotations.Default)]
public class QuotationAppService : ApplicationService, IQuotationAppService
{
    private readonly IRepository<Quotation, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public QuotationAppService(
        IRepository<Quotation, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<QuotationDto> GetAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        return await MapToDtoAsync(quotation);
    }

    public async Task<PagedResultDto<QuotationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var quotations = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "IssueDate DESC");

        var dtos = new System.Collections.Generic.List<QuotationDto>();
        foreach (var q in quotations)
        {
            dtos.Add(await MapToDtoAsync(q));
        }

        return new PagedResultDto<QuotationDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Quotations.Create)]
    public async Task<QuotationDto> CreateAsync(CreateQuotationDto input)
    {
        var quotationNumber = await _numberGenerator.GenerateAsync("Quotation", input.CompanyId);

        var quotation = new Quotation(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            quotationNumber,
            input.IssueDate);

        quotation.ValidUntil = input.ValidUntil;
        quotation.CurrencyCode = input.CurrencyCode;
        quotation.Terms = input.Terms;
        quotation.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            quotation.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(quotation, autoSave: true);
        return await MapToDtoAsync(quotation);
    }

    [Authorize(MyERPPermissions.Quotations.Submit)]
    public async Task<QuotationDto> SubmitAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        quotation.Submit();
        await _repository.UpdateAsync(quotation, autoSave: true);
        return await MapToDtoAsync(quotation);
    }

    [Authorize(MyERPPermissions.Quotations.Cancel)]
    public async Task<QuotationDto> CancelAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        quotation.Cancel();
        await _repository.UpdateAsync(quotation, autoSave: true);
        return await MapToDtoAsync(quotation);
    }

    private async Task<QuotationDto> MapToDtoAsync(Quotation quotation)
    {
        string? customerName = null;
        try
        {
            var customer = await _customerRepository.GetAsync(quotation.CustomerId);
            customerName = customer.Name;
        }
        catch { /* customer may not exist */ }

        return new QuotationDto
        {
            Id = quotation.Id,
            CompanyId = quotation.CompanyId,
            QuotationNumber = quotation.QuotationNumber,
            IssueDate = quotation.IssueDate,
            ValidUntil = quotation.ValidUntil,
            CustomerId = quotation.CustomerId,
            CustomerName = customerName,
            CurrencyCode = quotation.CurrencyCode,
            NetTotal = quotation.NetTotal,
            TaxAmount = quotation.TaxAmount,
            GrandTotal = quotation.GrandTotal,
            Terms = quotation.Terms,
            Notes = quotation.Notes,
            Status = quotation.Status.ToString(),
            ConvertedToSalesOrderId = quotation.ConvertedToSalesOrderId,
            CreationTime = quotation.CreationTime,
            LastModificationTime = quotation.LastModificationTime,
            Items = quotation.Items.Select(i => new QuotationItemDto
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
