using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.JournalEntries.Default)]
public class JournalEntryAppService : ApplicationService, IJournalEntryAppService
{
    private readonly IRepository<JournalEntry, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public JournalEntryAppService(
        IRepository<JournalEntry, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<JournalEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return MapToDto(entry);
    }

    public async Task<PagedResultDto<JournalEntryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var entries = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "PostingDate DESC");

        return new PagedResultDto<JournalEntryDto>(
            totalCount,
            entries.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<JournalEntryDto> CreateAsync(CreateJournalEntryDto input)
    {
        var entryNumber = await _numberGenerator.GenerateAsync("JournalEntry", input.CompanyId);

        var entry = new JournalEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            input.FiscalYearId,
            input.PostingDate);

        entry.EntryNumber = entryNumber;
        entry.ReferenceType = input.ReferenceType;
        entry.ReferenceId = input.ReferenceId;
        entry.ReferenceNumber = input.ReferenceNumber;
        entry.Narration = input.Narration;

        foreach (var line in input.Lines)
        {
            entry.AddLine(line.AccountId, line.Amount, line.IsDebit, line.Description);
        }

        // Validate double-entry balance before saving
        entry.Validate();

        await _repository.InsertAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.JournalEntries.Post)]
    public async Task<JournalEntryDto> PostAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Post();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.JournalEntries.Post)]
    public async Task<JournalEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    private static JournalEntryDto MapToDto(JournalEntry entry) => new()
    {
        Id = entry.Id,
        CompanyId = entry.CompanyId,
        FiscalYearId = entry.FiscalYearId,
        EntryNumber = entry.EntryNumber,
        PostingDate = entry.PostingDate,
        ReferenceType = entry.ReferenceType,
        ReferenceId = entry.ReferenceId,
        ReferenceNumber = entry.ReferenceNumber,
        Narration = entry.Narration,
        Status = entry.Status.ToString(),
        TotalDebit = entry.TotalDebit,
        TotalCredit = entry.TotalCredit,
        Lines = entry.Lines.Select(l => new JournalEntryLineDto
        {
            Id = l.Id,
            AccountId = l.AccountId,
            Amount = l.Amount,
            IsDebit = l.IsDebit,
            Description = l.Description
        }).ToList()
    };
}
