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

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesOrderAppService : ApplicationService, ISalesOrderAppService
{
    private readonly IRepository<SalesOrder, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public SalesOrderAppService(
        IRepository<SalesOrder, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<SalesOrderDto> GetAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        return await MapToDtoAsync(order);
    }

    public async Task<PagedResultDto<SalesOrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var orders = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "OrderDate DESC");

        var dtos = new System.Collections.Generic.List<SalesOrderDto>();
        foreach (var o in orders)
        {
            dtos.Add(await MapToDtoAsync(o));
        }

        return new PagedResultDto<SalesOrderDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<SalesOrderDto> CreateAsync(CreateSalesOrderDto input)
    {
        var orderNumber = await _numberGenerator.GenerateAsync("SalesOrder", input.CompanyId);

        var order = new SalesOrder(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            orderNumber,
            input.OrderDate);

        order.DeliveryDate = input.DeliveryDate;
        order.CustomerPoNumber = input.CustomerPoNumber;
        order.CurrencyCode = input.CurrencyCode;
        order.Terms = input.Terms;
        order.Notes = input.Notes;

        if (input.QuotationId.HasValue)
        {
            order.QuotationId = input.QuotationId;
        }

        foreach (var item in input.Items)
        {
            order.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(MyERPPermissions.SalesOrders.Submit)]
    public async Task<SalesOrderDto> SubmitAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        order.Submit();
        await _repository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(MyERPPermissions.SalesOrders.Cancel)]
    public async Task<SalesOrderDto> CancelAsync(Guid id)
    {
        var order = await _repository.GetAsync(id);
        order.Cancel();
        await _repository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    private async Task<SalesOrderDto> MapToDtoAsync(SalesOrder order)
    {
        string? customerName = null;
        try
        {
            var customer = await _customerRepository.GetAsync(order.CustomerId);
            customerName = customer.Name;
        }
        catch { /* customer may not exist */ }

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
            CreationTime = order.CreationTime,
            LastModificationTime = order.LastModificationTime,
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
}
