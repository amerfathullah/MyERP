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
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Manages Installation Notes — tracks equipment/product installation after delivery.
/// Linked to Delivery Notes; validates installation date >= DN posting date.
/// </summary>
[Authorize(MyERPPermissions.DeliveryNotes.Default)]
public class InstallationNoteAppService : ApplicationService, IInstallationNoteAppService
{
    private readonly IRepository<InstallationNote, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public InstallationNoteAppService(
        IRepository<InstallationNote, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
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
        var number = await _numberGenerator.GenerateAsync("IN", input.CompanyId);
        var note = new InstallationNote(
            GuidGenerator.Create(),
            input.CompanyId,
            number,
            input.CustomerId,
            input.DeliveryNoteId,
            input.InstallationDate,
            CurrentTenant.Id);

        foreach (var item in input.Items)
        {
            note.AddItem(item.ItemId, item.Qty, item.SerialNo);
        }

        await _repository.InsertAsync(note);
        return ObjectMapper.Map<InstallationNote, InstallationNoteDto>(note);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Submit)]
    public async Task SubmitAsync(Guid id)
    {
        var note = await _repository.GetAsync(id);
        note.Submit();
        await _repository.UpdateAsync(note);
    }

    [Authorize(MyERPPermissions.DeliveryNotes.Cancel)]
    public async Task CancelAsync(Guid id)
    {
        var note = await _repository.GetAsync(id);
        note.Cancel();
        await _repository.UpdateAsync(note);
    }
}
