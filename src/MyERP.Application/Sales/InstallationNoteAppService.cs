using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Manages Installation Notes — tracks equipment/product installation after delivery.
/// Linked to Delivery Notes; validates installation date >= DN posting date and installed
/// qty <= DN qty (per DO-NOT on both InstallationNote and its ValidateInstallationDate method).
/// </summary>
[Authorize(MyERPPermissions.DeliveryNotes.Default)]
public class InstallationNoteAppService : ApplicationService, IInstallationNoteAppService
{
    private readonly IRepository<InstallationNote, Guid> _repository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public InstallationNoteAppService(
        IRepository<InstallationNote, Guid> repository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<InstallationNoteDto> GetAsync(Guid id)
    {
        var note = await _repository.GetAsync(id);
        return ObjectMapper.Map<InstallationNote, InstallationNoteDto>(note);
    }

    public async Task<PagedResultDto<InstallationNoteDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.InstallationNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.InstallationDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<InstallationNoteDto>(count, list.Select(x => ObjectMapper.Map<InstallationNote, InstallationNoteDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Create)]
    public async Task<InstallationNoteDto> CreateAsync(CreateInstallationNoteDto input)
    {
        if (input.Items == null || input.Items.Count == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToArray();
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        var deliveryNote = await _deliveryNoteRepository.GetAsync(input.DeliveryNoteId, includeDetails: true);
        if (deliveryNote.CompanyId != input.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch);
        }

        // Per ERPNext PR #61806 / commit 6128ae894d: validate DN status, return flag, and customer match
        if (deliveryNote.Status == DocumentStatus.Cancelled || deliveryNote.IsReturn)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Installation Note cannot be created against a cancelled or return Delivery Note.");
        }

        if (deliveryNote.CustomerId != input.CustomerId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Installation Note customer must match Delivery Note customer.");
        }

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "InstallationNote", input.CompanyId, itemIds: itemIds, customerIds: new[] { input.CustomerId });

        var number = await _numberGenerator.GenerateAsync("IN", input.CompanyId);
        var note = new InstallationNote(
            GuidGenerator.Create(),
            input.CompanyId,
            number,
            input.CustomerId,
            input.DeliveryNoteId,
            input.InstallationDate,
            CurrentTenant.Id);

        // Per DO-NOT on InstallationNote: cannot install before the DN was delivered.
        note.ValidateInstallationDate(deliveryNote.PostingDate);

        foreach (var item in input.Items)
        {
            var dnItemId = item.DeliveryNoteItemId;
            if (dnItemId.HasValue)
            {
                if (!deliveryNote.Items.Any(d => d.Id == dnItemId.Value && d.ItemId == item.ItemId))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Delivery Note Item {dnItemId.Value} does not belong to Delivery Note {deliveryNote.DeliveryNumber}.");
                }
            }
            else
            {
                dnItemId = deliveryNote.Items.FirstOrDefault(d => d.ItemId == item.ItemId)?.Id;
            }
            note.AddItem(item.ItemId, item.Qty, item.SerialNo, dnItemId);
        }

        // Per DO-NOT: installed qty must never exceed the DN's item qty, across all
        // (non-cancelled) Installation Notes raised against this DN.
        await ValidateItemQtyAsync(deliveryNote, note);

        await _repository.InsertAsync(note);
        return ObjectMapper.Map<InstallationNote, InstallationNoteDto>(note);
    }

    private async Task ValidateItemQtyAsync(DeliveryNote deliveryNote, InstallationNote note)
    {
        var query = await _repository.GetQueryableAsync();
        var priorNotes = query
            .Where(n => n.DeliveryNoteId == deliveryNote.Id
                     && n.Id != note.Id
                     && n.Status != DocumentStatus.Cancelled)
            .ToList();

        var alreadyInstalledByItem = priorNotes
            .SelectMany(n => n.Items)
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Qty));

        // Per ERPNext PR #61806: deduct returned qty from DN item qty when validating installable qty
        var dnQtyByItem = deliveryNote.Items
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Max(0m, i.Quantity - Math.Abs(i.ReturnedQty))));

        foreach (var item in note.Items)
        {
            var dnQty = dnQtyByItem.GetValueOrDefault(item.ItemId);
            var alreadyInstalled = alreadyInstalledByItem.GetValueOrDefault(item.ItemId);
            var totalInstalled = alreadyInstalled + item.Qty;

            if (totalInstalled > dnQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InstallationQtyExceedsDeliveryNote)
                    .WithData("itemId", item.ItemId)
                    .WithData("deliveryNoteQty", dnQty)
                    .WithData("alreadyInstalled", alreadyInstalled)
                    .WithData("newQty", item.Qty);
            }
        }
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Submit)]
    public async Task SubmitAsync(Guid id)
    {
        var note = await _repository.GetAsync(id);
        var deliveryNote = await _deliveryNoteRepository.GetAsync(note.DeliveryNoteId, includeDetails: true);

        // Per ERPNext PR #61806 / commit d2f0e2981a: validate installable limit on submit in case returns occurred after creation
        await ValidateItemQtyAsync(deliveryNote, note);

        note.Submit();
        await _repository.UpdateAsync(note);

        // Update DeliveryNoteItem.InstalledQty and DeliveryNote status (per ERPNext status_updater)
        foreach (var item in note.Items)
        {
            DeliveryNoteItem? dnItem = null;
            if (item.DeliveryNoteItemId.HasValue)
            {
                dnItem = deliveryNote.Items.FirstOrDefault(d => d.Id == item.DeliveryNoteItemId.Value);
            }
            dnItem ??= deliveryNote.Items.FirstOrDefault(d => d.ItemId == item.ItemId);
            if (dnItem != null)
            {
                dnItem.InstalledQty += item.Qty;
            }
        }
        await _deliveryNoteRepository.UpdateAsync(deliveryNote, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "InstallationNote", note.Id,
            "Submitted", note.CompanyId,
            note.InstallationNumber, "Draft", "Submitted", CurrentUser.Id,
            $"Installation Note {note.InstallationNumber} submitted", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Cancel)]
    public async Task CancelAsync(Guid id)
    {
        var note = await _repository.GetAsync(id);
        note.Cancel();
        await _repository.UpdateAsync(note);

        // Reverse DeliveryNoteItem.InstalledQty (per ERPNext status_updater)
        var deliveryNote = await _deliveryNoteRepository.GetAsync(note.DeliveryNoteId, includeDetails: true);
        foreach (var item in note.Items)
        {
            DeliveryNoteItem? dnItem = null;
            if (item.DeliveryNoteItemId.HasValue)
            {
                dnItem = deliveryNote.Items.FirstOrDefault(d => d.Id == item.DeliveryNoteItemId.Value);
            }
            dnItem ??= deliveryNote.Items.FirstOrDefault(d => d.ItemId == item.ItemId);
            if (dnItem != null)
            {
                dnItem.InstalledQty = Math.Max(0, dnItem.InstalledQty - item.Qty);
            }
        }
        await _deliveryNoteRepository.UpdateAsync(deliveryNote, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "InstallationNote", note.Id,
            "Cancelled", note.CompanyId,
            note.InstallationNumber, "Submitted", "Cancelled", CurrentUser.Id,
            $"Installation Note {note.InstallationNumber} cancelled", CurrentTenant.Id));
    }
}
