using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace MyERP.Core.BackgroundJobs;

/// <summary>
/// Background job that creates recurring invoices from Auto-Repeat entries.
/// Fires nightly per company. Creates a Draft copy of the template SI/PI for each due repeat.
/// Per ERPNext: creates as Draft (never auto-submits), per DO-NOT: cannot auto-repeat cancelled documents.
/// Per ERPNext PR #60256 (commit f6dfd4336d): keep payment terms, PO no, and shift service dates into new billing period.
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
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepository;
    private readonly IRepository<PaymentScheduleEntry, Guid> _paymentScheduleRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<RecurringInvoiceJob> _logger;
    private readonly AutoRepeatService _autoRepeatService;

    public RecurringInvoiceJob(
        IRepository<AutoRepeat, Guid> autoRepeatRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentTermsTemplate, Guid> paymentTermsRepository,
        IRepository<PaymentScheduleEntry, Guid> paymentScheduleRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ILogger<RecurringInvoiceJob> logger,
        AutoRepeatService autoRepeatService)
    {
        _autoRepeatRepository = autoRepeatRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _paymentTermsRepository = paymentTermsRepository;
        _paymentScheduleRepository = paymentScheduleRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _logger = logger;
        _autoRepeatService = autoRepeatService;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(RecurringInvoiceJobArgs args)
    {
        // Cleanup: disable expired auto-repeats before processing
        await _autoRepeatService.DisableExpiredAsync(DateTime.UtcNow);

        var dueRepeats = await _autoRepeatService.GetDueAutoRepeatsAsync(args.AsOfDate, args.CompanyId);

        // Filter to SalesInvoice and PurchaseInvoice repeats
        dueRepeats = dueRepeats
            .Where(ar => ar.ReferenceDocumentType == "SalesInvoice" || ar.ReferenceDocumentType == "PurchaseInvoice")
            .ToList();

        if (!dueRepeats.Any())
            return;

        int created = 0;
        foreach (var repeat in dueRepeats)
        {
            try
            {
                if (repeat.ReferenceDocumentType == "SalesInvoice")
                {
                    await CreateRecurringSalesInvoiceAsync(repeat, args);
                }
                else if (repeat.ReferenceDocumentType == "PurchaseInvoice")
                {
                    await CreateRecurringPurchaseInvoiceAsync(repeat, args);
                }
                await _autoRepeatService.RecordGenerationAsync(repeat.Id, args.AsOfDate);
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

    private async Task CreateRecurringSalesInvoiceAsync(AutoRepeat repeat, RecurringInvoiceJobArgs args)
    {
        // Load the template invoice (FindAsync — template may have been deleted)
        var template = await _salesInvoiceRepository.FindAsync(repeat.ReferenceDocumentId);

        // Template deleted: auto-disable and stop retrying
        if (template == null)
        {
            _logger.LogWarning(
                "Recurring invoice template {TemplateId} has been deleted, disabling AutoRepeat {RepeatId}",
                repeat.ReferenceDocumentId, repeat.Id);
            repeat.IsEnabled = false;
            return;
        }

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
            args.AsOfDate, // posting date = schedule date
            args.TenantId);

        newInvoice.CurrencyCode = template.CurrencyCode;
        newInvoice.ExchangeRate = template.ExchangeRate;
        newInvoice.Notes = $"Auto-generated from recurring template {repeat.ReferenceDocumentNumber}";
        newInvoice.ProjectId = template.ProjectId;
        newInvoice.CostCenterId = template.CostCenterId;
        newInvoice.DebitToAccountId = template.DebitToAccountId;
        newInvoice.CustomerPoNumber = template.CustomerPoNumber; // Per ERPNext PR #60256 (commit f6dfd4336d)
        newInvoice.WriteOffAmount = template.WriteOffAmount; // Per ERPNext on_recurring
        newInvoice.PaymentTermsTemplateId = template.PaymentTermsTemplateId; // Per PR #60256
        newInvoice.DueDate = null; // Rebuilt from template relative to new posting date
        newInvoice.UpdateStock = false; // Default false for recurring draft invoice

        DateTime? newFromDate = null;
        DateTime? newToDate = null;
        if (template.FromDate.HasValue && template.ToDate.HasValue)
        {
            (newFromDate, newToDate) = CalculateNextPeriod(
                template.FromDate.Value, template.ToDate.Value, args.AsOfDate, repeat.Frequency);
            newInvoice.FromDate = newFromDate;
            newInvoice.ToDate = newToDate;
        }

        // Copy items from template and shift service dates
        foreach (var item in template.Items)
        {
            newInvoice.AddItem(
                item.ItemId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.TaxAmount,
                item.Uom);

            var newItem = newInvoice.Items.Last();
            newItem.StockUom = item.StockUom;
            newItem.ConversionFactor = item.ConversionFactor;
            newItem.EnableDeferredRevenue = item.EnableDeferredRevenue;
            newItem.DeferredRevenueAccountId = item.DeferredRevenueAccountId;

            // Shift service dates per ERPNext PR #60256 (commit f6dfd4336d)
            if (template.FromDate.HasValue && template.ToDate.HasValue && newFromDate.HasValue && newToDate.HasValue)
            {
                if (item.ServiceStartDate.HasValue)
                {
                    newItem.ServiceStartDate = ShiftDate(
                        item.ServiceStartDate.Value,
                        template.FromDate.Value,
                        template.ToDate.Value,
                        newFromDate.Value,
                        newToDate.Value,
                        repeat.Frequency);
                }
                if (item.ServiceEndDate.HasValue)
                {
                    newItem.ServiceEndDate = ShiftDate(
                        item.ServiceEndDate.Value,
                        template.FromDate.Value,
                        template.ToDate.Value,
                        newFromDate.Value,
                        newToDate.Value,
                        repeat.Frequency);
                }
            }
            else if (item.ServiceStartDate.HasValue || item.ServiceEndDate.HasValue)
            {
                if (item.ServiceStartDate.HasValue)
                {
                    newItem.ServiceStartDate = ShiftDateByFrequency(
                        item.ServiceStartDate.Value, repeat.Frequency, args.AsOfDate, template.IssueDate);
                }
                if (item.ServiceEndDate.HasValue)
                {
                    newItem.ServiceEndDate = ShiftDateByFrequency(
                        item.ServiceEndDate.Value, repeat.Frequency, args.AsOfDate, template.IssueDate);
                }
            }
        }

        // Rebuild payment schedule from template relative to new posting date (PR #60256)
        if (newInvoice.PaymentTermsTemplateId.HasValue)
        {
            var termsTemplate = await _paymentTermsRepository.FindAsync(newInvoice.PaymentTermsTemplateId.Value);
            if (termsTemplate != null)
            {
                var schedule = termsTemplate.GenerateSchedule(args.AsOfDate, newInvoice.GrandTotal);
                if (schedule.Count > 0)
                {
                    newInvoice.DueDate = schedule.Max(s => s.DueDate);
                }

                await _salesInvoiceRepository.InsertAsync(newInvoice, autoSave: true);

                foreach (var line in schedule)
                {
                    var basePaymentAmount = Math.Round(line.PaymentAmount * newInvoice.ExchangeRate, 2);
                    var entry = new PaymentScheduleEntry(
                        _guidGenerator.Create(), "SalesInvoice", newInvoice.Id,
                        line.DueDate, line.InvoicePortion, line.PaymentAmount,
                        line.Description)
                    {
                        BasePaymentAmount = basePaymentAmount,
                        TenantId = newInvoice.TenantId,
                    };
                    await _paymentScheduleRepository.InsertAsync(entry);
                }
                return;
            }
        }

        await _salesInvoiceRepository.InsertAsync(newInvoice, autoSave: true);
    }

    private async Task CreateRecurringPurchaseInvoiceAsync(AutoRepeat repeat, RecurringInvoiceJobArgs args)
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
            repeat.IsEnabled = false;
            return;
        }

        var invoiceNumber = await _numberGenerator.GenerateAsync("PurchaseInvoice", repeat.CompanyId);

        var newInvoice = new PurchaseInvoice(
            _guidGenerator.Create(),
            template.CompanyId,
            template.SupplierId,
            invoiceNumber,
            args.AsOfDate,
            args.TenantId);

        newInvoice.CurrencyCode = template.CurrencyCode;
        newInvoice.ExchangeRate = template.ExchangeRate;
        newInvoice.Notes = $"Auto-generated from recurring template {repeat.ReferenceDocumentNumber}";
        newInvoice.CostCenterId = template.CostCenterId;
        newInvoice.CreditToAccountId = template.CreditToAccountId;
        newInvoice.WriteOffAmount = template.WriteOffAmount;
        newInvoice.PaymentTermsTemplateId = template.PaymentTermsTemplateId;
        newInvoice.DueDate = null;
        newInvoice.UpdateStock = false;

        DateTime? newFromDate = null;
        DateTime? newToDate = null;
        if (template.FromDate.HasValue && template.ToDate.HasValue)
        {
            (newFromDate, newToDate) = CalculateNextPeriod(
                template.FromDate.Value, template.ToDate.Value, args.AsOfDate, repeat.Frequency);
            newInvoice.FromDate = newFromDate;
            newInvoice.ToDate = newToDate;
        }

        foreach (var item in template.Items)
        {
            newInvoice.AddItem(
                item.ItemId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.TaxAmount,
                item.Uom);

            var newItem = newInvoice.Items.Last();
            newItem.StockUom = item.StockUom;
            newItem.ConversionFactor = item.ConversionFactor;
            newItem.EnableDeferredExpense = item.EnableDeferredExpense;
            newItem.DeferredExpenseAccountId = item.DeferredExpenseAccountId;

            if (template.FromDate.HasValue && template.ToDate.HasValue && newFromDate.HasValue && newToDate.HasValue)
            {
                if (item.ServiceStartDate.HasValue)
                {
                    newItem.ServiceStartDate = ShiftDate(
                        item.ServiceStartDate.Value,
                        template.FromDate.Value,
                        template.ToDate.Value,
                        newFromDate.Value,
                        newToDate.Value,
                        repeat.Frequency);
                }
                if (item.ServiceEndDate.HasValue)
                {
                    newItem.ServiceEndDate = ShiftDate(
                        item.ServiceEndDate.Value,
                        template.FromDate.Value,
                        template.ToDate.Value,
                        newFromDate.Value,
                        newToDate.Value,
                        repeat.Frequency);
                }
            }
            else if (item.ServiceStartDate.HasValue || item.ServiceEndDate.HasValue)
            {
                if (item.ServiceStartDate.HasValue)
                {
                    newItem.ServiceStartDate = ShiftDateByFrequency(
                        item.ServiceStartDate.Value, repeat.Frequency, args.AsOfDate, template.IssueDate);
                }
                if (item.ServiceEndDate.HasValue)
                {
                    newItem.ServiceEndDate = ShiftDateByFrequency(
                        item.ServiceEndDate.Value, repeat.Frequency, args.AsOfDate, template.IssueDate);
                }
            }
        }

        if (newInvoice.PaymentTermsTemplateId.HasValue)
        {
            var termsTemplate = await _paymentTermsRepository.FindAsync(newInvoice.PaymentTermsTemplateId.Value);
            if (termsTemplate != null)
            {
                var schedule = termsTemplate.GenerateSchedule(args.AsOfDate, newInvoice.GrandTotal);
                if (schedule.Count > 0)
                {
                    newInvoice.DueDate = schedule.Max(s => s.DueDate);
                }

                await _purchaseInvoiceRepository.InsertAsync(newInvoice, autoSave: true);

                foreach (var line in schedule)
                {
                    var basePaymentAmount = Math.Round(line.PaymentAmount * newInvoice.ExchangeRate, 2);
                    var entry = new PaymentScheduleEntry(
                        _guidGenerator.Create(), "PurchaseInvoice", newInvoice.Id,
                        line.DueDate, line.InvoicePortion, line.PaymentAmount,
                        line.Description)
                    {
                        BasePaymentAmount = basePaymentAmount,
                        TenantId = newInvoice.TenantId,
                    };
                    await _paymentScheduleRepository.InsertAsync(entry);
                }
                return;
            }
        }

        await _purchaseInvoiceRepository.InsertAsync(newInvoice, autoSave: true);
    }

    /// <summary>
    /// Calculates the next billing period [newFromDate, newToDate] matching repeat frequency.
    /// Preserves full-month alignment (e.g. 01-31 Jan becomes 01-28 Feb).
    /// </summary>
    public static (DateTime FromDate, DateTime ToDate) CalculateNextPeriod(
        DateTime refFrom, DateTime refTo, DateTime postingDate, RepeatFrequency frequency)
    {
        bool shiftByMonths = frequency switch
        {
            RepeatFrequency.Monthly => true,
            RepeatFrequency.Quarterly => true,
            RepeatFrequency.HalfYearly => true,
            RepeatFrequency.Yearly => true,
            _ => false
        };

        if (shiftByMonths)
        {
            int stepMonths = frequency switch
            {
                RepeatFrequency.Quarterly => 3,
                RepeatFrequency.HalfYearly => 6,
                RepeatFrequency.Yearly => 12,
                _ => 1
            };

            int months = (postingDate.Year - refFrom.Year) * 12 + (postingDate.Month - refFrom.Month);
            if (months <= 0) months = stepMonths;

            var newFrom = refFrom.AddMonths(months);
            DateTime newTo;

            bool wasEndOfMonth = refTo.Date == new DateTime(refFrom.Year, refFrom.Month, DateTime.DaysInMonth(refFrom.Year, refFrom.Month));
            if (wasEndOfMonth)
            {
                newTo = new DateTime(newFrom.Year, newFrom.Month, DateTime.DaysInMonth(newFrom.Year, newFrom.Month));
            }
            else
            {
                newTo = refTo.AddMonths(months);
            }

            if (newTo < newFrom) newTo = newFrom;
            return (newFrom, newTo);
        }
        else
        {
            int stepDays = frequency == RepeatFrequency.Daily ? 1 : 7;
            int days = (int)(postingDate.Date - refFrom.Date).TotalDays;
            if (days <= 0) days = stepDays;

            var newFrom = refFrom.AddDays(days);
            var newTo = refTo.AddDays(days);
            return (newFrom, newTo);
        }
    }

    /// <summary>
    /// Shift item service dates into the new invoice period.
    /// Per ERPNext PR #60256 (commit f6dfd4336d / accounts_controller.py):
    /// - Keep the period end aligned (reference_to_date becomes target to_date).
    /// - Whole months never reverse a period (e.g. 29-31 Jan becomes 28-28 Feb).
    /// - Dates inside the reference period stay inside the new period (min(shifted, to_date)).
    /// </summary>
    public static DateTime ShiftDate(
        DateTime date,
        DateTime referenceFromDate,
        DateTime referenceToDate,
        DateTime fromDate,
        DateTime toDate,
        RepeatFrequency frequency)
    {
        var d = date.Date;
        var refTo = referenceToDate.Date;
        var targetTo = toDate.Date;
        var refFrom = referenceFromDate.Date;
        var targetFrom = fromDate.Date;

        if (d == refTo)
        {
            return targetTo;
        }

        bool shiftByMonths = frequency switch
        {
            RepeatFrequency.Monthly => true,
            RepeatFrequency.Quarterly => true,
            RepeatFrequency.HalfYearly => true,
            RepeatFrequency.Yearly => true,
            _ => false
        };

        if (!shiftByMonths)
        {
            var days = (int)(targetFrom - refFrom).TotalDays;
            return d.AddDays(days);
        }

        int months = (targetFrom.Year - refFrom.Year) * 12 + (targetFrom.Month - refFrom.Month);
        var shifted = d.AddMonths(months);

        // Month ends stay month ends, e.g. 1-28 Feb becomes 1-31 Mar (ERPNext PR #60296 / commit 1b709ff29a)
        if (d.Day == DateTime.DaysInMonth(d.Year, d.Month))
        {
            shifted = new DateTime(shifted.Year, shifted.Month, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        }

        // Dates inside the reference period stay inside the new period, which can end earlier in the month.
        if (d < refTo)
        {
            return shifted < targetTo ? shifted : targetTo;
        }

        // Dates after the reference period stay after the new period, which can end later in the month.
        var afterPeriod = targetTo.AddDays(1);
        return shifted > afterPeriod ? shifted : afterPeriod;
    }

    public static DateTime ShiftDateByFrequency(
        DateTime date, RepeatFrequency frequency, DateTime newPostingDate, DateTime refPostingDate)
    {
        bool shiftByMonths = frequency switch
        {
            RepeatFrequency.Monthly => true,
            RepeatFrequency.Quarterly => true,
            RepeatFrequency.HalfYearly => true,
            RepeatFrequency.Yearly => true,
            _ => false
        };

        if (shiftByMonths)
        {
            int months = (newPostingDate.Year - refPostingDate.Year) * 12 + (newPostingDate.Month - refPostingDate.Month);
            if (months <= 0)
            {
                months = frequency switch
                {
                    RepeatFrequency.Quarterly => 3,
                    RepeatFrequency.HalfYearly => 6,
                    RepeatFrequency.Yearly => 12,
                    _ => 1
                };
            }
            return date.AddMonths(months);
        }
        else
        {
            int days = (int)(newPostingDate.Date - refPostingDate.Date).TotalDays;
            if (days <= 0) days = frequency == RepeatFrequency.Daily ? 1 : 7;
            return date.AddDays(days);
        }
    }
}
