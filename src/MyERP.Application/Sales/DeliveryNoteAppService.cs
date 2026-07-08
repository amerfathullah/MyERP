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

[Authorize(MyERPPermissions.DeliveryNotes.Default)]
public class DeliveryNoteAppService : ApplicationService, IDeliveryNoteAppService
{
    private readonly IRepository<DeliveryNote, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public DeliveryNoteAppService(
        IRepository<DeliveryNote, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<DeliveryNoteDto> GetAsync(Guid id)
    {
        var dn = await _repository.GetAsync(id);
        return MapToDto(dn);
    }

    public async Task<PagedResultDto<DeliveryNoteDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var list = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "PostingDate DESC");

        return new PagedResultDto<DeliveryNoteDto>(
            totalCount,
            list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto input)
    {
        var deliveryNumber = await _numberGenerator.GenerateAsync("DeliveryNote", input.CompanyId);

        var dn = new DeliveryNote(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            input.WarehouseId,
            deliveryNumber,
            input.PostingDate,
            CurrentTenant.Id);

        dn.SalesOrderId = input.SalesOrderId;
        dn.ShippingAddress = input.ShippingAddress;
        dn.Transporter = input.Transporter;
        dn.TrackingNumber = input.TrackingNumber;
        dn.IsReturn = input.IsReturn;
        dn.ReturnAgainstId = input.ReturnAgainstId;
        dn.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            dn.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.SalesOrderItemId);
        }

        await _repository.InsertAsync(dn, autoSave: true);
        return MapToDto(dn);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Submit)]
    public async Task<DeliveryNoteDto> SubmitAsync(Guid id)
    {
        var dn = await _repository.GetAsync(id);
        dn.Submit();
        await _repository.UpdateAsync(dn, autoSave: true);
        return MapToDto(dn);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Cancel)]
    public async Task<DeliveryNoteDto> CancelAsync(Guid id)
    {
        var dn = await _repository.GetAsync(id);
        dn.Cancel();
        await _repository.UpdateAsync(dn, autoSave: true);
        return MapToDto(dn);
    }

    private static DeliveryNoteDto MapToDto(DeliveryNote dn) => new()
    {
        Id = dn.Id,
        CompanyId = dn.CompanyId,
        DeliveryNumber = dn.DeliveryNumber,
        PostingDate = dn.PostingDate,
        CustomerId = dn.CustomerId,
        SalesOrderId = dn.SalesOrderId,
        WarehouseId = dn.WarehouseId,
        ShippingAddress = dn.ShippingAddress,
        Transporter = dn.Transporter,
        TrackingNumber = dn.TrackingNumber,
        CurrencyCode = dn.CurrencyCode,
        NetTotal = dn.NetTotal,
        TaxAmount = dn.TaxAmount,
        GrandTotal = dn.GrandTotal,
        IsReturn = dn.IsReturn,
        ReturnAgainstId = dn.ReturnAgainstId,
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
            SalesOrderItemId = i.SalesOrderItemId
        }).ToList()
    };
}
