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
/// Background job that creates recurring sales orders from Auto-Repeat entries.
/// Fires nightly per company. Creates a Draft copy of the template SO for each due repeat.
/// Per ERPNext: creates as Draft (never auto-submits), per DO-NOT: cannot auto-repeat cancelled documents.
/// </summary>
public class RecurringSalesOrderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
}

public class RecurringSalesOrderJob : AsyncBackgroundJob<RecurringSalesOrderJobArgs>, ITransientDependency
{
    private readonly IRepository<AutoRepeat, Guid> _autoRepeatRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<RecurringSalesOrderJob> _logger;
    private readonly AutoRepeatService _autoRepeatService;

    public RecurringSalesOrderJob(
        IRepository<AutoRepeat, Guid> autoRepeatRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ILogger<RecurringSalesOrderJob> logger,
        AutoRepeatService autoRepeatService)
    {
        _autoRepeatRepository = autoRepeatRepository;
        _salesOrderRepository = salesOrderRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _logger = logger;
        _autoRepeatService = autoRepeatService;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(RecurringSalesOrderJobArgs args)
    {
        await _autoRepeatService.DisableExpiredAsync(DateTime.UtcNow);

        var dueRepeats = (await _autoRepeatService.GetDueAutoRepeatsAsync(args.AsOfDate, args.CompanyId))
            .Where(ar => ar.ReferenceDocumentType == "SalesOrder")
            .ToList();

        if (!dueRepeats.Any())
            return;

        int created = 0;
        foreach (var repeat in dueRepeats)
        {
            try
            {
                await CreateRecurringSalesOrderAsync(repeat, args);
                await _autoRepeatService.RecordGenerationAsync(repeat.Id, args.AsOfDate);
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create recurring sales order for AutoRepeat {Id} (template: {Template})",
                    repeat.Id, repeat.ReferenceDocumentNumber);
                // Per-repeat isolation: one failure doesn't block others
            }
        }

        if (created > 0)
        {
            _logger.LogInformation(
                "Created {Count} recurring sales order(s) for company {CompanyId}",
                created, args.CompanyId);
        }
    }

    private async Task CreateRecurringSalesOrderAsync(AutoRepeat repeat, RecurringSalesOrderJobArgs args)
    {
        var template = await _salesOrderRepository.FindAsync(repeat.ReferenceDocumentId);

        if (template == null)
        {
            _logger.LogWarning(
                "Recurring sales order template {TemplateId} has been deleted, disabling AutoRepeat {RepeatId}",
                repeat.ReferenceDocumentId, repeat.Id);
            repeat.IsEnabled = false;
            return;
        }

        if (template.Status == DocumentStatus.Cancelled)
        {
            repeat.IsEnabled = false; // auto-disable on cancelled template
            return;
        }

        var orderNumber = await _numberGenerator.GenerateAsync("SalesOrder", repeat.CompanyId);

        var newOrder = new SalesOrder(
            _guidGenerator.Create(),
            template.CompanyId,
            template.CustomerId,
            orderNumber,
            args.AsOfDate, // order date = today
            args.TenantId);

        newOrder.CurrencyCode = template.CurrencyCode;
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

        await _salesOrderRepository.InsertAsync(newOrder, autoSave: true);
    }
}
