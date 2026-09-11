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
        if (input.ToCaseNo < input.FromCaseNo)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.Items == null || input.Items.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToArray();
        var itemValidation = LazyServiceProvider?.LazyGetService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        if (itemValidation != null)
        {
            await itemValidation.ValidateItemsForTransactionAsync(itemIds);
        }

        var dn = await _deliveryNoteRepository.FindAsync(input.DeliveryNoteId);
        if (dn == null)
            throw new BusinessException("MyERP:01004")
                .WithData("entity", "DeliveryNote");
        if (dn.Status != Core.DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Packing Slip can only be created for Draft Delivery Notes.");

        // Case number overlap validation — PackingSlip.HasOverlap() already implements the
        // 3-condition check, it just had no call site. Only Submitted slips count: a Draft
        // slip isn't a final case-range commitment yet (matches ERPNext's own check scope).
        var existingSlipsQuery = await _repository.GetQueryableAsync();
        var existingSlips = existingSlipsQuery
            .Where(ps => ps.DeliveryNoteId == input.DeliveryNoteId && ps.Status == Core.DocumentStatus.Submitted)
            .ToList();
        var overlapping = existingSlips.FirstOrDefault(ps =>
            PackingSlip.HasOverlap(input.FromCaseNo, input.ToCaseNo, ps.FromCaseNo, ps.ToCaseNo));
        if (overlapping != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CaseNumberRangeOverlap)
                .WithData("fromCaseNo", input.FromCaseNo)
                .WithData("toCaseNo", input.ToCaseNo)
                .WithData("existingSlipId", overlapping.Id);
        }

        // Per ERPNext packing_slip.py validate_items: a row referencing a DN item cannot pack more
        // than that line's remaining (unpacked) quantity — DN_Item.Quantity - already-accumulated
        // PackedQty from other submitted slips. Without this, cumulative Packing Slips against one
        // DN line can exceed what was ever ordered/shipped on it.
        foreach (var itemDto in input.Items.Where(i => i.DeliveryNoteItemId.HasValue))
        {
            var dnItem = dn.Items.FirstOrDefault(i => i.Id == itemDto.DeliveryNoteItemId!.Value);
            if (dnItem == null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Delivery Note Item reference does not exist on the selected Delivery Note.");
            }

            var remainingQty = dnItem.Quantity - dnItem.PackedQty;
            if (itemDto.Qty > remainingQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Qty cannot be greater than {remainingQty} for Item {dnItem.ItemId} — Packing Slip already covers the rest of this Delivery Note line.");
            }
        }

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
            var description = itemDto.Description;
            if (string.IsNullOrWhiteSpace(description))
            {
                var itemEntity = await _itemRepository.FindAsync(itemDto.ItemId);
                description = itemEntity?.ItemName ?? itemEntity?.ItemCode;
            }

            entity.AddItem(itemDto.ItemId, itemDto.Qty, itemDto.NetWeight, description);
            // AddItem has no DeliveryNoteItemId parameter — without this, the field a Packing Slip
            // Item carries for exactly this purpose was always left null, which silently made both
            // AdjustParentDeliveryNotePackedQtyAsync (DN PackedQty write-back on submit/cancel) and
            // the over-pack guard above permanently unreachable dead code.
            entity.Items.Last().DeliveryNoteItemId = itemDto.DeliveryNoteItemId;
        }

        await _repository.InsertAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.PackingSlips.Submit)]
    public async Task<PackingSlipDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);

        var dn = await _deliveryNoteRepository.GetAsync(entity.DeliveryNoteId);
        if (dn.Status != Core.DocumentStatus.Draft)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Packing Slip can only be submitted for Draft Delivery Notes.");
        }

        entity.Submit();
        await _repository.UpdateAsync(entity);

        // Increment DN item PackedQty for each row this slip covers — the DN's own submit gate
        // (Delivery Note item not fully packed) reads this field once any slip is submitted.
        await AdjustParentDeliveryNotePackedQtyAsync(entity, sign: 1);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PackingSlip", entity.Id,
            "Submitted", entity.CompanyId,
            entity.Id.ToString()[..8], "Draft", "Submitted", CurrentUser.Id,
            $"Packing Slip ({entity.Id.ToString()[..8]}) submitted", CurrentTenant.Id));

        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.PackingSlips.Cancel)]
    public async Task<PackingSlipDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity);

        await AdjustParentDeliveryNotePackedQtyAsync(entity, sign: -1);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PackingSlip", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.Id.ToString()[..8], "Submitted", "Cancelled", CurrentUser.Id,
            $"Packing Slip ({entity.Id.ToString()[..8]}) cancelled", CurrentTenant.Id));

        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.PackingSlips.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.Status != Core.DocumentStatus.Draft)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft packing slips can be deleted");
        }

        await _repository.DeleteAsync(entity);
    }

    /// <summary>
    /// Computes the next suggested case number for a Delivery Note: MAX(ToCaseNo) + 1, or 1 if none (Gotcha #128).
    /// </summary>
    public async Task<int> GetNextCaseNoAsync(Guid deliveryNoteId)
    {
        var slipsQuery = await _repository.GetQueryableAsync();
        var maxToCaseNo = slipsQuery
            .Where(ps => ps.DeliveryNoteId == deliveryNoteId && ps.Status != Core.DocumentStatus.Cancelled)
            .Select(ps => (int?)ps.ToCaseNo)
            .Max();

        return (maxToCaseNo ?? 0) + 1;
    }

    /// <summary>
    /// Applies (sign=1, on submit) or reverses (sign=-1, on cancel) this slip's item quantities
    /// onto the parent DN's item PackedQty. Only rows with a direct DeliveryNoteItemId reference
    /// are counted — Packed Item (bundle-component) rows have no DN item to attribute to.
    /// </summary>
    private async Task AdjustParentDeliveryNotePackedQtyAsync(PackingSlip entity, int sign)
    {
        var dnItemRows = entity.Items.Where(i => i.DeliveryNoteItemId.HasValue).ToList();
        if (dnItemRows.Count == 0) return;

        var dn = await _deliveryNoteRepository.GetAsync(entity.DeliveryNoteId);
        foreach (var row in dnItemRows)
        {
            var dnItem = dn.Items.FirstOrDefault(i => i.Id == row.DeliveryNoteItemId!.Value);
            if (dnItem == null) continue;
            dnItem.PackedQty = Math.Max(0, dnItem.PackedQty + sign * row.Qty);
        }

        await _deliveryNoteRepository.UpdateAsync(dn, autoSave: true);
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
