using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.PackingSlips.Default)]
public class PackingSlipAppService : ApplicationService, IPackingSlipAppService
{
    private readonly IRepository<PackingSlip, Guid> _repository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public PackingSlipAppService(
        IRepository<PackingSlip, Guid> repository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _repository = repository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _itemRepository = itemRepository;
    }

    public async Task<PackingSlipDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return await MapToDtoAsync(entity);
    }

    public async Task<PagedResultDto<PackingSlipDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();

        var items = query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = new List<PackingSlipDto>();
        foreach (var item in items)
        {
            dtos.Add(await MapToDtoAsync(item));
        }

        return new PagedResultDto<PackingSlipDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.PackingSlips.Create)]
    public async Task<PackingSlipDto> CreateAsync(CreatePackingSlipDto input)
    {
        var dn = await _deliveryNoteRepository.FindAsync(input.DeliveryNoteId);
        if (dn == null)
            throw new BusinessException("MyERP:01004")
                .WithData("entity", "DeliveryNote");

        var entity = new PackingSlip(
            GuidGenerator.Create(),
            input.CompanyId,
            input.DeliveryNoteId,
            input.FromCaseNo,
            input.ToCaseNo,
            CurrentTenant.Id);

        entity.GrossWeight = input.GrossWeightKg;
        entity.WeightUom = input.WeightUom ?? "Kg";

        foreach (var itemDto in input.Items)
        {
            entity.AddItem(itemDto.ItemId, itemDto.Qty, itemDto.NetWeight, itemDto.Description);
        }

        await _repository.InsertAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.PackingSlips.Submit)]
    public async Task<PackingSlipDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.PackingSlips.Cancel)]
    public async Task<PackingSlipDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.PackingSlips.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.Status != Core.DocumentStatus.Draft)
            throw new BusinessException("MyERP:01001");
        await _repository.DeleteAsync(entity);
    }

    private async Task<PackingSlipDto> MapToDtoAsync(PackingSlip entity)
    {
        var dto = new PackingSlipDto
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            DeliveryNoteId = entity.DeliveryNoteId,
            FromCaseNo = entity.FromCaseNo,
            ToCaseNo = entity.ToCaseNo,
            NumberOfCases = entity.NumberOfCases,
            NetWeightKg = entity.NetWeight,
            GrossWeightKg = entity.GrossWeight,
            WeightUom = entity.WeightUom,
            Status = (int)entity.Status,
            CreationTime = entity.CreationTime
        };

        // Resolve delivery note number
        var dn = await _deliveryNoteRepository.FindAsync(entity.DeliveryNoteId);
        dto.DeliveryNoteNumber = dn?.DeliveryNumber;

        // Map items with item details
        foreach (var item in entity.Items)
        {
            var itemEntity = await _itemRepository.FindAsync(item.ItemId);
            dto.Items.Add(new PackingSlipItemDto
            {
                Id = item.Id,
                ItemId = item.ItemId,
                ItemCode = itemEntity?.ItemCode,
                ItemName = itemEntity?.ItemName,
                Qty = item.Qty,
                NetWeight = item.NetWeight,
                Description = item.Description,
                DeliveryNoteItemId = item.DeliveryNoteItemId
            });
        }

        return dto;
    }
}
