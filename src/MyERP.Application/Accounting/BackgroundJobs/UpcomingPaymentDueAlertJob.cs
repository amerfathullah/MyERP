using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Notification;
using MyERP.Notification.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Proactive alert for upcoming payment due dates (both payables and receivables).
/// Notifies AP team about supplier invoices due within 3/7 days for cash flow planning.
/// Notifies AR team about customer invoices due within 3/7 days for collection follow-up.
/// Per ERPNext: send_payment_reminders daily scheduler + payment_due_date alerts.
/// Separate from PaymentReminderJob which handles OVERDUE invoices.
/// </summary>
public class UpcomingPaymentDueAlertJob : AsyncBackgroundJob<UpcomingPaymentDueAlertJobArgs>, ITransientDependency
{
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly ILogger<UpcomingPaymentDueAlertJob> _logger;

    public UpcomingPaymentDueAlertJob(
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<AppNotification, Guid> notificationRepository,
        ILogger<UpcomingPaymentDueAlertJob> logger)
    {
        _piRepository = piRepository;
        _siRepository = siRepository;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(UpcomingPaymentDueAlertJobArgs args)
    {
        _logger.LogInformation("UpcomingPaymentDueAlertJob: Checking company {CompanyId}", args.CompanyId);

        var today = args.AsOfDate.Date;
        var threeDaysFromNow = today.AddDays(3);
        var sevenDaysFromNow = today.AddDays(7);

        // --- Payables (supplier invoices due soon) ---
        var piQueryable = await _piRepository.GetQueryableAsync();
        var upcomingPayables = piQueryable
            .Where(pi => pi.CompanyId == args.CompanyId
                && pi.Status == DocumentStatus.Posted
                && !pi.IsReturn
                && pi.DueDate != null
                && pi.DueDate >= today
                && pi.DueDate <= sevenDaysFromNow
                && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount) > 0.01m)
            .Select(pi => new
            {
                pi.Id,
                pi.InvoiceNumber,
                pi.DueDate,
                Outstanding = pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount,
                DaysUntilDue = (pi.DueDate!.Value - today).Days
            })
            .OrderBy(pi => pi.DueDate)
            .ToList();

        // --- Receivables (customer invoices due soon — for collection follow-up) ---
        var siQueryable = await _siRepository.GetQueryableAsync();
        var upcomingReceivables = siQueryable
            .Where(si => si.CompanyId == args.CompanyId
                && si.Status == DocumentStatus.Posted
                && !si.IsReturn
                && si.DueDate != null
                && si.DueDate >= today
                && si.DueDate <= sevenDaysFromNow
                && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m)
            .Select(si => new
            {
                si.Id,
                si.InvoiceNumber,
                si.DueDate,
                Outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance,
                DaysUntilDue = (si.DueDate!.Value - today).Days
            })
            .OrderBy(si => si.DueDate)
            .ToList();

        if (upcomingPayables.Count == 0 && upcomingReceivables.Count == 0)
        {
            _logger.LogDebug("UpcomingPaymentDueAlertJob: No upcoming dues for company {CompanyId}", args.CompanyId);
            return;
        }

        // Create payables notification (AP team)
        if (upcomingPayables.Count > 0)
        {
            var urgentPayables = upcomingPayables.Count(p => p.DaysUntilDue <= 3);
            var totalPayable = upcomingPayables.Sum(p => p.Outstanding);
            var topPayables = upcomingPayables.Take(5)
                .Select(p => $"{p.InvoiceNumber}: due in {p.DaysUntilDue}d ({p.Outstanding:N2})")
                .ToList();

            var payableMessage = $"{upcomingPayables.Count} supplier invoice(s) due within 7 days " +
                $"({urgentPayables} within 3 days), total {totalPayable:N2}. " +
                $"Top: {string.Join("; ", topPayables)}";

            var payableNotification = new AppNotification(
                Guid.NewGuid(),
                args.UserId,
                payableMessage,
                args.TenantId)
            {
                Severity = urgentPayables > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                SourceDocumentType = "PurchaseInvoice"
            };

            await _notificationRepository.InsertAsync(payableNotification);
        }

        // Create receivables notification (AR team — collection follow-up)
        if (upcomingReceivables.Count > 0)
        {
            var urgentReceivables = upcomingReceivables.Count(r => r.DaysUntilDue <= 3);
            var totalReceivable = upcomingReceivables.Sum(r => r.Outstanding);
            var topReceivables = upcomingReceivables.Take(5)
                .Select(r => $"{r.InvoiceNumber}: due in {r.DaysUntilDue}d ({r.Outstanding:N2})")
                .ToList();

            var receivableMessage = $"{upcomingReceivables.Count} customer invoice(s) due within 7 days " +
                $"({urgentReceivables} within 3 days), total {totalReceivable:N2}. " +
                $"Follow up: {string.Join("; ", topReceivables)}";

            var receivableNotification = new AppNotification(
                Guid.NewGuid(),
                args.UserId,
                receivableMessage,
                args.TenantId)
            {
                Severity = NotificationSeverity.Info,
                SourceDocumentType = "SalesInvoice"
            };

            await _notificationRepository.InsertAsync(receivableNotification);
        }

        _logger.LogInformation(
            "UpcomingPaymentDueAlertJob: Company {CompanyId} — {PayableCount} payables ({PayableTotal:N2}), {ReceivableCount} receivables ({ReceivableTotal:N2}) due within 7 days",
            args.CompanyId,
            upcomingPayables.Count, upcomingPayables.Sum(p => p.Outstanding),
            upcomingReceivables.Count, upcomingReceivables.Sum(r => r.Outstanding));
    }
}

public class UpcomingPaymentDueAlertJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
    public Guid UserId { get; set; }
}
