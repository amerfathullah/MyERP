using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace MyERP.Core.BackgroundJobs;

/// <summary>
/// Background job that creates recurring purchase invoices from Auto-Repeat entries.
/// Fires nightly per company. Creates a Draft copy of the template PI for each due repeat.
/// Per ERPNext: creates as Draft (never auto-submits), per DO-NOT: cannot auto-repeat cancelled documents.
/// </summary>
public class RecurringPurchaseInvoiceJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
}

public class RecurringPurchaseInvoiceJob : AsyncBackgroundJob<RecurringPurchaseInvoiceJobArgs>, ITransientDependency
{
    private readonly IRepository<AutoRepeat, Guid> _autoRepeatRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<RecurringPurchaseInvoiceJob> _logger;
    private readonly AutoRepeatService _autoRepeatService;

    public RecurringPurchaseInvoiceJob(
        IRepository<AutoRepeat, Guid> autoRepeatRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ILogger<RecurringPurchaseInvoiceJob> logger,
        AutoRepeatService autoRepeatService)
    {
        _autoRepeatRepository = autoRepeatRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _logger = logger;
        _autoRepeatService = autoRepeatService;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(RecurringPurchaseInvoiceJobArgs args)
    {
        await _autoRepeatService.DisableExpiredAsync(DateTime.UtcNow);

        var dueRepeats = (await _autoRepeatService.GetDueAutoRepeatsAsync(args.AsOfDate, args.CompanyId))
            .Where(ar => ar.ReferenceDocumentType == "PurchaseInvoice")
            .ToList();

        if (!dueRepeats.Any())
            return;

        int created = 0;
        foreach (var repeat in dueRepeats)
        {
            try
            {
                await CreateRecurringPurchaseInvoiceAsync(repeat, args);
                await _autoRepeatService.RecordGenerationAsync(repeat.Id, args.AsOfDate);
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create recurring purchase invoice for AutoRepeat {Id} (template: {Template})",
                    repeat.Id, repeat.ReferenceDocumentNumber);
                // Per-repeat isolation: one failure doesn't block others
            }
        }

        if (created > 0)
        {
            _logger.LogInformation(
                "Created {Count} recurring purchase invoice(s) for company {CompanyId}",
                created, args.CompanyId);
        }
    }

    private async Task CreateRecurringPurchaseInvoiceAsync(AutoRepeat repeat, RecurringPurchaseInvoiceJobArgs args)
    {
        var template = await _purchaseInvoiceRepository.FindAsync(repeat.ReferenceDocumentId);

        if (template == null)
        {
            _logger.LogWarning(
                "Recurring purchase invoice template {TemplateId} has been deleted, disabling AutoRepeat {RepeatId}",
                repeat.ReferenceDocumentId, repeat.Id);
            repeat.IsEnabled = false;
            return;
        }

        if (template.Status == DocumentStatus.Cancelled)
        {
            repeat.IsEnabled = false; // auto-disable on cancelled template
            return;
        }

        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", repeat.CompanyId);

        var newInvoice = new PurchaseInvoice(
            _guidGenerator.Create(),
            template.CompanyId,
            template.SupplierId,
            invoiceNumber,
            args.AsOfDate, // issue date = today
            args.TenantId);

        newInvoice.CurrencyCode = template.CurrencyCode;
        newInvoice.ExchangeRate = template.ExchangeRate;
        newInvoice.PriceListId = template.PriceListId;
        newInvoice.CreditToAccountId = template.CreditToAccountId;
        newInvoice.PaymentTermsTemplateId = template.PaymentTermsTemplateId;
        newInvoice.Notes = $"Auto-generated from recurring template {repeat.ReferenceDocumentNumber}";

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

        await _purchaseInvoiceRepository.InsertAsync(newInvoice, autoSave: true);
    }
}
