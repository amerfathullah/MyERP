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
/// Background job that creates recurring purchase orders from Auto-Repeat entries.
/// Fires nightly per company. Creates a Draft copy of the template PO for each due repeat.
/// Per ERPNext: creates as Draft (never auto-submits), per DO-NOT: cannot auto-repeat cancelled documents.
/// </summary>
public class RecurringPurchaseOrderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
}

public class RecurringPurchaseOrderJob : AsyncBackgroundJob<RecurringPurchaseOrderJobArgs>, ITransientDependency
{
    private readonly IRepository<AutoRepeat, Guid> _autoRepeatRepository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<RecurringPurchaseOrderJob> _logger;
    private readonly AutoRepeatService _autoRepeatService;

    public RecurringPurchaseOrderJob(
        IRepository<AutoRepeat, Guid> autoRepeatRepository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ILogger<RecurringPurchaseOrderJob> logger,
        AutoRepeatService autoRepeatService)
    {
        _autoRepeatRepository = autoRepeatRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _logger = logger;
        _autoRepeatService = autoRepeatService;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(RecurringPurchaseOrderJobArgs args)
    {
        await _autoRepeatService.DisableExpiredAsync(DateTime.UtcNow);

        var dueRepeats = (await _autoRepeatService.GetDueAutoRepeatsAsync(args.AsOfDate, args.CompanyId))
            .Where(ar => ar.ReferenceDocumentType == "PurchaseOrder")
            .ToList();

        if (!dueRepeats.Any())
            return;

        int created = 0;
        foreach (var repeat in dueRepeats)
        {
            try
            {
                await CreateRecurringPurchaseOrderAsync(repeat, args);
                await _autoRepeatService.RecordGenerationAsync(repeat.Id, args.AsOfDate);
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create recurring purchase order for AutoRepeat {Id} (template: {Template})",
                    repeat.Id, repeat.ReferenceDocumentNumber);
                // Per-repeat isolation: one failure doesn't block others
            }
        }

        if (created > 0)
        {
            _logger.LogInformation(
                "Created {Count} recurring purchase order(s) for company {CompanyId}",
                created, args.CompanyId);
        }
    }

    private async Task CreateRecurringPurchaseOrderAsync(AutoRepeat repeat, RecurringPurchaseOrderJobArgs args)
    {
        var template = await _purchaseOrderRepository.FindAsync(repeat.ReferenceDocumentId);

        if (template == null)
        {
            _logger.LogWarning(
                "Recurring purchase order template {TemplateId} has been deleted, disabling AutoRepeat {RepeatId}",
                repeat.ReferenceDocumentId, repeat.Id);
            repeat.IsEnabled = false;
            return;
        }

        if (template.Status == DocumentStatus.Cancelled)
        {
            repeat.IsEnabled = false; // auto-disable on cancelled template
            return;
        }

        var orderNumber = await _numberGenerator.GenerateAsync("PurchaseOrder", repeat.CompanyId);

        var newOrder = new PurchaseOrder(
            _guidGenerator.Create(),
            template.CompanyId,
            template.SupplierId,
            orderNumber,
            args.AsOfDate, // order date = today
            args.TenantId);

        newOrder.CurrencyCode = template.CurrencyCode;
        newOrder.ExchangeRate = template.ExchangeRate;
        newOrder.PriceListId = template.PriceListId;
        newOrder.ProjectId = template.ProjectId;
        newOrder.Notes = $"Auto-generated from recurring template {repeat.ReferenceDocumentNumber}";

        foreach (var item in template.Items)
        {
            newOrder.AddItem(
                item.ItemId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.TaxAmount,
                item.Uom);
        }

        await _purchaseOrderRepository.InsertAsync(newOrder, autoSave: true);
    }
}
