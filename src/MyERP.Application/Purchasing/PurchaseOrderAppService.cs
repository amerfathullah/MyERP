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

[Authorize(MyERPPermissions.Suppliers.Default)]
public class PurchaseOrderAppService : ApplicationService
{
    private readonly IRepository<PurchaseOrder, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PurchaseOrderAppService(
        IRepository<PurchaseOrder, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PurchaseOrderDto> GetAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        return MapToDto(po);
    }

    public async Task<PagedResultDto<PurchaseOrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var count = await _repository.GetCountAsync();
        var list = await _repository.GetPagedListAsync(input.SkipCount, input.MaxResultCount, input.Sorting ?? "OrderDate DESC");
        return new PagedResultDto<PurchaseOrderDto>(count, list.Select(MapToDto).ToList());
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto input)
    {
        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", input.CompanyId);
        var po = new PurchaseOrder(GuidGenerator.Create(), input.CompanyId, input.SupplierId, orderNumber, input.OrderDate);
        po.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        po.Notes = input.Notes;

        foreach (var item in input.Items)
            po.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);

        await _repository.InsertAsync(po, autoSave: true);
        return MapToDto(po);
    }

    public async Task<PurchaseOrderDto> SubmitAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        po.Submit();
        await _repository.UpdateAsync(po, autoSave: true);
        return MapToDto(po);
    }

    public async Task<PurchaseOrderDto> CancelAsync(Guid id)
    {
        var po = await _repository.GetAsync(id);
        po.Cancel();
        await _repository.UpdateAsync(po, autoSave: true);
        return MapToDto(po);
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po) => new()
    {
        Id = po.Id,
        CompanyId = po.CompanyId,
        OrderNumber = po.OrderNumber,
        OrderDate = po.OrderDate,
        ExpectedDeliveryDate = po.ExpectedDeliveryDate,
        SupplierId = po.SupplierId,
        NetTotal = po.NetTotal,
        TaxAmount = po.TaxAmount,
        GrandTotal = po.GrandTotal,
        Status = po.Status.ToString(),
        Items = po.Items.Select(i => new PurchaseOrderItemDto
        {
            Id = i.Id, ItemId = i.ItemId, Description = i.Description,
            Uom = i.Uom, Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            TaxAmount = i.TaxAmount, LineTotal = i.LineTotal
        }).ToList()
    };
}
