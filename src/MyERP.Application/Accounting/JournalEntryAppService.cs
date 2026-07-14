using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.JournalEntries.Default)]
public class JournalEntryAppService : ApplicationService, IJournalEntryAppService
{
    private readonly IRepository<JournalEntry, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<AccountingPeriod, Guid> _periodRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public JournalEntryAppService(
        IRepository<JournalEntry, Guid> repository,
        IDocumentNumberGenerator numberGenerator,
        IRepository<Company, Guid> companyRepository,
        IRepository<AccountingPeriod, Guid> periodRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
        _companyRepository = companyRepository;
        _periodRepository = periodRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<JournalEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return MapToDto(entry);
    }

    public async Task<PagedResultDto<JournalEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.EntryNumber != null && x.EntryNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var entries = query
            .OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

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

        // Validate accounting period is not closed/frozen
        var company = await _companyRepository.GetAsync(entry.CompanyId);
        if (company.AccountsFrozenTillDate.HasValue && entry.PostingDate <= company.AccountsFrozenTillDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("frozenTill", company.AccountsFrozenTillDate.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"));
        }

        // Check fiscal year exists and is open
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == entry.CompanyId && f.StartDate <= entry.PostingDate && f.EndDate >= entry.PostingDate);
        if (fy != null && fy.IsClosed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"))
                .WithData("fiscalYear", fy.Name);
        }

        // Check closed accounting period
        var periodQuery = await _periodRepository.GetQueryableAsync();
        var closedPeriod = periodQuery.FirstOrDefault(p =>
            p.IsClosed && p.CompanyId == entry.CompanyId
            && p.StartDate <= entry.PostingDate && p.EndDate >= entry.PostingDate);
        if (closedPeriod != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("period", closedPeriod.PeriodName)
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"));
        }

        entry.Post();
        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntry", entry.Id, "Posted",
            entry.CompanyId, entry.EntryNumber, "Draft", "Posted",
            CurrentUser.Id, tenantId: entry.TenantId));

        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.JournalEntries.Post)]
    public async Task<JournalEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed (reversals can't post to locked periods)
        var company = await _companyRepository.GetAsync(entry.CompanyId);
        if (company.AccountsFrozenTillDate.HasValue && entry.PostingDate <= company.AccountsFrozenTillDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("frozenTill", company.AccountsFrozenTillDate.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"));
        }

        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntry", entry.Id, "Cancelled",
            entry.CompanyId, entry.EntryNumber, "Posted", "Cancelled",
            CurrentUser.Id, tenantId: entry.TenantId));

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
