using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace MyERP.Core.BackgroundJobs;

/// <summary>
/// Background job that creates recurring invoices from Auto-Repeat entries.
/// Fires nightly per company. Creates a Draft copy of the template SI for each due repeat.
/// Per ERPNext: creates as Draft (never auto-submits), per DO-NOT: cannot auto-repeat cancelled documents.
/// </summary>
public class RecurringInvoiceJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
}

public class RecurringInvoiceJob : AsyncBackgroundJob<RecurringInvoiceJobArgs>, ITransientDependency
{
    private readonly IRepository<AutoRepeat, Guid> _autoRepeatRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<RecurringInvoiceJob> _logger;

    public RecurringInvoiceJob(
        IRepository<AutoRepeat, Guid> autoRepeatRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ILogger<RecurringInvoiceJob> logger)
    {
        _autoRepeatRepository = autoRepeatRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(RecurringInvoiceJobArgs args)
    {
        var query = await _autoRepeatRepository.GetQueryableAsync();
        var dueRepeats = query
            .Where(ar => ar.CompanyId == args.CompanyId
                && ar.IsEnabled
                && ar.NextScheduleDate <= args.AsOfDate
                && ar.ReferenceDocumentType == "SalesInvoice")
            .ToList();

        if (!dueRepeats.Any())
            return;

        int created = 0;
        foreach (var repeat in dueRepeats)
        {
            try
            {
                await CreateRecurringInvoiceAsync(repeat, args);
                repeat.RecordGeneration(args.AsOfDate);
                await _autoRepeatRepository.UpdateAsync(repeat);
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create recurring invoice for AutoRepeat {Id} (template: {Template})",
                    repeat.Id, repeat.ReferenceDocumentNumber);
                // Per-repeat isolation: one failure doesn't block others
            }
        }

        if (created > 0)
        {
            _logger.LogInformation(
                "Created {Count} recurring invoice(s) for company {CompanyId}",
                created, args.CompanyId);
        }
    }

    private async Task CreateRecurringInvoiceAsync(AutoRepeat repeat, RecurringInvoiceJobArgs args)
    {
        // Load the template invoice
        var template = await _salesInvoiceRepository.GetAsync(repeat.ReferenceDocumentId);

        // Per DO-NOT: cannot auto-repeat cancelled documents
        if (template.Status == DocumentStatus.Cancelled)
        {
            repeat.IsEnabled = false; // auto-disable on cancelled template
            return;
        }

        // Generate new invoice number
        var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", repeat.CompanyId);

        // Create new draft invoice from template
        var newInvoice = new SalesInvoice(
            _guidGenerator.Create(),
            template.CompanyId,
            template.CustomerId,
            invoiceNumber,
            args.AsOfDate, // posting date = today
            args.TenantId);

        newInvoice.CurrencyCode = template.CurrencyCode;
        newInvoice.ExchangeRate = template.ExchangeRate;
        newInvoice.Notes = $"Auto-generated from recurring template {repeat.ReferenceDocumentNumber}";
        newInvoice.ProjectId = template.ProjectId;

        // Copy items from template
        foreach (var item in template.Items)
        {
            newInvoice.AddItem(
                item.ItemId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.TaxAmount,
                item.Uom);
        }

        // Copy payment terms if available
        newInvoice.PaymentTermsTemplateId = template.PaymentTermsTemplateId;

        await _salesInvoiceRepository.InsertAsync(newInvoice, autoSave: true);
    }
}
