using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace MyERP.Core.BackgroundJobs;

/// <summary>
/// Background job that creates recurring journal entries from Auto-Repeat entries.
/// Per ERPNext: Auto-Repeat supports JournalEntry for monthly accruals (rent, insurance, depreciation).
/// Creates Draft JEs from template, user must review and post manually.
/// </summary>
public class RecurringJournalEntryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
}

public class RecurringJournalEntryJob : AsyncBackgroundJob<RecurringJournalEntryJobArgs>, ITransientDependency
{
    private readonly IRepository<AutoRepeat, Guid> _autoRepeatRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<Accounting.Entities.FiscalYear, Guid> _fiscalYearRepository;
    private readonly AutoRepeatService _autoRepeatService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<RecurringJournalEntryJob> _logger;

    public RecurringJournalEntryJob(
        IRepository<AutoRepeat, Guid> autoRepeatRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<Accounting.Entities.FiscalYear, Guid> fiscalYearRepository,
        AutoRepeatService autoRepeatService,
        IGuidGenerator guidGenerator,
        ILogger<RecurringJournalEntryJob> logger)
    {
        _autoRepeatRepository = autoRepeatRepository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _autoRepeatService = autoRepeatService;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(RecurringJournalEntryJobArgs args)
    {
        var dueRepeats = await _autoRepeatService.GetDueAutoRepeatsAsync(args.AsOfDate, args.CompanyId);

        // Filter to JournalEntry type only
        var jeRepeats = dueRepeats
            .Where(ar => ar.ReferenceDocumentType == "JournalEntry")
            .ToList();

        if (!jeRepeats.Any())
        {
            _logger.LogDebug("RecurringJournalEntryJob: No due JE auto-repeats for company {CompanyId}", args.CompanyId);
            return;
        }

        var createdCount = 0;

        foreach (var repeat in jeRepeats)
        {
            try
            {
                var template = await _journalEntryRepository.FindAsync(repeat.ReferenceDocumentId);
                if (template == null)
                {
                    // Template deleted — auto-disable the repeat
                    repeat.Disable();
                    await _autoRepeatRepository.UpdateAsync(repeat, autoSave: true);
                    _logger.LogWarning("RecurringJournalEntryJob: Template JE {TemplateId} not found, disabling auto-repeat {RepeatId}",
                        repeat.ReferenceDocumentId, repeat.Id);
                    continue;
                }

                // Resolve fiscal year for the posting date
                var postingDate = args.AsOfDate;
                var fiscalYear = (await _fiscalYearRepository.GetListAsync(fy =>
                    fy.CompanyId == args.CompanyId &&
                    fy.StartDate <= postingDate &&
                    fy.EndDate >= postingDate))
                    .FirstOrDefault();

                if (fiscalYear == null)
                {
                    _logger.LogWarning("RecurringJournalEntryJob: No fiscal year covers {PostingDate} for company {CompanyId}, skipping",
                        postingDate, args.CompanyId);
                    continue;
                }

                // Create new JE from template
                var newJe = new JournalEntry(
                    _guidGenerator.Create(),
                    template.CompanyId,
                    fiscalYear.Id,
                    postingDate,
                    template.TenantId);

                newJe.VoucherType = template.VoucherType;
                newJe.EntryNumber = $"REC-JE-{postingDate:yyyyMMdd}-{createdCount + 1:D3}";

                // Copy all lines from template with same DR/CR amounts
                foreach (var line in template.Lines)
                {
                    newJe.AddLine(line.AccountId, line.Amount, line.IsDebit,
                        $"Auto-generated from recurring template (Period: {postingDate:MMM yyyy})");
                    var newLine = newJe.Lines.Last();
                    newLine.CostCenterId = line.CostCenterId;
                    newLine.ProjectId = line.ProjectId;
                    newLine.FinanceBook = line.FinanceBook;
                }

                await _journalEntryRepository.InsertAsync(newJe, autoSave: true);

                // Advance the repeat schedule
                await _autoRepeatService.RecordGenerationAsync(repeat.Id, args.AsOfDate);
                createdCount++;

                _logger.LogInformation("RecurringJournalEntryJob: Created JE {EntryNumber} from template {TemplateId}",
                    newJe.EntryNumber, template.Id);
            }
            catch (Exception ex)
            {
                // Per-repeat error isolation — one failure doesn't block others
                _logger.LogError(ex, "RecurringJournalEntryJob: Error processing auto-repeat {RepeatId}", repeat.Id);
            }
        }

        _logger.LogInformation("RecurringJournalEntryJob: Created {Count} recurring JEs for company {CompanyId}",
            createdCount, args.CompanyId);
    }
}
