using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using MyERP.Core.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Manages Installation Notes — tracks equipment/product installation after delivery.
/// Linked to Delivery Notes; validates installation date >= DN posting date.
/// </summary>
[Authorize(MyERPPermissions.DeliveryNotes.Default)]
public class InstallationNoteAppService : ApplicationService
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

#region DTOs

public class InstallationNoteDto
{
    public Guid Id { get; set; }
    public string InstallationNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public DateTime InstallationDate { get; set; }
    public string Status { get; set; } = null!;
    public List<InstallationNoteItemDto> Items { get; set; } = new();
}

public class InstallationNoteItemDto
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public string? SerialNo { get; set; }
}

public class CreateInstallationNoteDto
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public DateTime InstallationDate { get; set; }
    public List<InstallationNoteItemDto> Items { get; set; } = new();
}

#endregion
