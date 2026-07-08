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

[Authorize(MyERPPermissions.PurchaseReceipts.Default)]
public class PurchaseReceiptAppService : ApplicationService, IPurchaseReceiptAppService
{
    private readonly IRepository<PurchaseReceipt, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public PurchaseReceiptAppService(
        IRepository<PurchaseReceipt, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PurchaseReceiptDto> GetAsync(Guid id)
    {
        var receipt = await _repository.GetAsync(id);
        return MapToDto(receipt);
    }

    public async Task<PagedResultDto<PurchaseReceiptDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var list = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "PostingDate DESC");

        return new PagedResultDto<PurchaseReceiptDto>(
            totalCount,
            list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Create)]
    public async Task<PurchaseReceiptDto> CreateAsync(CreatePurchaseReceiptDto input)
    {
        var receiptNumber = await _numberGenerator.GenerateAsync("PurchaseReceipt", input.CompanyId);

        var receipt = new PurchaseReceipt(
            GuidGenerator.Create(),
            input.CompanyId,
            input.SupplierId,
            input.WarehouseId,
            receiptNumber,
            input.PostingDate,
            CurrentTenant.Id);

        receipt.PurchaseOrderId = input.PurchaseOrderId;
        receipt.SupplierDeliveryNote = input.SupplierDeliveryNote;
        receipt.IsReturn = input.IsReturn;
        receipt.ReturnAgainstId = input.ReturnAgainstId;
        receipt.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            receipt.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom, item.PurchaseOrderItemId);
        }

        await _repository.InsertAsync(receipt, autoSave: true);
        return MapToDto(receipt);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Submit)]
    public async Task<PurchaseReceiptDto> SubmitAsync(Guid id)
    {
        var receipt = await _repository.GetAsync(id);
        receipt.Submit();
        await _repository.UpdateAsync(receipt, autoSave: true);
        return MapToDto(receipt);
    }

    [Authorize(MyERPPermissions.PurchaseReceipts.Cancel)]
    public async Task<PurchaseReceiptDto> CancelAsync(Guid id)
    {
        var receipt = await _repository.GetAsync(id);
        receipt.Cancel();
        await _repository.UpdateAsync(receipt, autoSave: true);
        return MapToDto(receipt);
    }

    private static PurchaseReceiptDto MapToDto(PurchaseReceipt r) => new()
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        ReceiptNumber = r.ReceiptNumber,
        PostingDate = r.PostingDate,
        SupplierId = r.SupplierId,
        PurchaseOrderId = r.PurchaseOrderId,
        WarehouseId = r.WarehouseId,
        SupplierDeliveryNote = r.SupplierDeliveryNote,
        CurrencyCode = r.CurrencyCode,
        NetTotal = r.NetTotal,
        TaxAmount = r.TaxAmount,
        GrandTotal = r.GrandTotal,
        IsReturn = r.IsReturn,
        ReturnAgainstId = r.ReturnAgainstId,
        Status = r.Status.ToString(),
        Items = r.Items.Select(i => new PurchaseReceiptItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            Description = i.Description,
            Uom = i.Uom,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TaxAmount = i.TaxAmount,
            LineTotal = i.LineTotal,
            PurchaseOrderItemId = i.PurchaseOrderItemId
        }).ToList()
    };
}
