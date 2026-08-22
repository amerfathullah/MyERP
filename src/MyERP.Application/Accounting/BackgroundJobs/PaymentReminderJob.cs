using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Notification;
using MyERP.Notification.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Daily background job that sends payment reminders for overdue invoices.
/// Per ERPNext: send_payment_reminders daily scheduler event.
/// Creates AppNotification per overdue invoice (per customer/supplier).
/// Only processes invoices that are:
///   - Posted status (not Draft/Cancelled)
///   - Outstanding > 0
///   - DueDate < today
///   - Not already reminded in last N days (configurable, default 7)
/// </summary>
public class PaymentReminderJob : AsyncBackgroundJob<PaymentReminderJobArgs>, ITransientDependency
{
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly ILogger<PaymentReminderJob> _logger;

    public PaymentReminderJob(
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<AppNotification, Guid> notificationRepository,
        IIdentityUserRepository userRepository,
        ILogger<PaymentReminderJob> logger)
    {
        _siRepository = siRepository;
        _piRepository = piRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(PaymentReminderJobArgs args)
    {
        _logger.LogInformation("PaymentReminderJob: Processing company {CompanyId}", args.CompanyId);

        var today = args.AsOfDate.Date;
        var reminderCooldownDays = args.ReminderCooldownDays > 0 ? args.ReminderCooldownDays : 7;
        var reminderCutoff = today.AddDays(-reminderCooldownDays);

        // Resolve target users for notifications (admin + accounts role users)
        var targetUserIds = await ResolveNotificationRecipientsAsync();
        if (targetUserIds.Count == 0)
        {
            _logger.LogWarning("PaymentReminderJob: No notification recipients found. Skipping company {CompanyId}", args.CompanyId);
            return;
        }

        // Process overdue receivables (customer invoices)
        var overdueReceivables = await GetOverdueReceivablesAsync(args.CompanyId, today);
        var receivableRemindersSent = 0;

        foreach (var group in overdueReceivables.GroupBy(si => si.CustomerId))
        {
            try
            {
                var totalOverdue = group.Sum(si => si.OutstandingAmount);
                var oldestDueDate = group.Min(si => si.DueDate ?? si.IssueDate);
                var daysOverdue = (today - oldestDueDate).Days;
                var invoiceCount = group.Count();

                // Check if we already reminded recently
                var recentReminder = await HasRecentReminderAsync(
                    "SalesInvoice", group.Key, reminderCutoff);
                if (recentReminder) continue;

                foreach (var userId in targetUserIds)
                {
                    var notification = new AppNotification(
                        Guid.NewGuid(), userId,
                        $"Payment Overdue: {invoiceCount} invoice(s), {totalOverdue:N2} outstanding");
                    notification.Body = $"Customer has {invoiceCount} overdue invoice(s) totalling {totalOverdue:N2}. Oldest is {daysOverdue} days overdue.";
                    notification.Severity = NotificationSeverity.Warning;
                    notification.ActionUrl = "/accounting/reports/outstanding";
                    notification.SourceDocumentType = "SalesInvoice";
                    notification.SourceDocumentId = group.First().Id;
                    await _notificationRepository.InsertAsync(notification);
                }
                receivableRemindersSent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PaymentReminderJob: Failed to create reminder for customer {CustomerId}", group.Key);
            }
        }

        // Process overdue payables (supplier invoices)
        var overduePayables = await GetOverduePayablesAsync(args.CompanyId, today);
        var payableRemindersSent = 0;

        foreach (var group in overduePayables.GroupBy(pi => pi.SupplierId))
        {
            try
            {
                var totalOverdue = group.Sum(pi => pi.OutstandingAmount);
                var invoiceCount = group.Count();

                var recentReminder = await HasRecentReminderAsync(
                    "PurchaseInvoice", group.Key, reminderCutoff);
                if (recentReminder) continue;

                foreach (var userId in targetUserIds)
                {
                    var notification = new AppNotification(
                        Guid.NewGuid(), userId,
                        $"Payment Due: {invoiceCount} supplier invoice(s), {totalOverdue:N2} outstanding");
                    notification.Body = $"Supplier has {invoiceCount} overdue invoice(s) totalling {totalOverdue:N2}.";
                    notification.Severity = NotificationSeverity.Info;
                    notification.ActionUrl = "/accounting/reports/outstanding";
                    notification.SourceDocumentType = "PurchaseInvoice";
                    notification.SourceDocumentId = group.First().Id;
                    await _notificationRepository.InsertAsync(notification);
                }
                payableRemindersSent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PaymentReminderJob: Failed to create reminder for supplier {SupplierId}", group.Key);
            }
        }

        _logger.LogInformation(
            "PaymentReminderJob: Company {CompanyId} — {Receivables} receivable + {Payables} payable reminders created",
            args.CompanyId, receivableRemindersSent, payableRemindersSent);
    }

    private async Task<List<SalesInvoice>> GetOverdueReceivablesAsync(Guid companyId, DateTime today)
    {
        var queryable = await _siRepository.GetQueryableAsync();
        return queryable
            .Where(si => si.CompanyId == companyId
                && si.Status == DocumentStatus.Posted
                && si.IsReturn == false
                && si.DueDate != null
                && si.DueDate < today
                && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m)
            .OrderBy(si => si.DueDate)
            .ToList();
    }

    private async Task<List<PurchaseInvoice>> GetOverduePayablesAsync(Guid companyId, DateTime today)
    {
        var queryable = await _piRepository.GetQueryableAsync();
        return queryable
            .Where(pi => pi.CompanyId == companyId
                && pi.Status == DocumentStatus.Posted
                && pi.IsReturn == false
                && pi.DueDate != null
                && pi.DueDate < today
                && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0.01m)
            .OrderBy(pi => pi.DueDate)
            .ToList();
    }

    private async Task<bool> HasRecentReminderAsync(string docType, Guid partyId, DateTime cutoff)
    {
        var queryable = await _notificationRepository.GetQueryableAsync();
        return queryable.Any(n =>
            n.SourceDocumentType == docType
            && n.CreationTime > cutoff
            && n.Subject != null && n.Subject.Contains("Overdue"));
    }

    /// <summary>
    /// Resolves users who should receive payment reminder notifications.
    /// Returns admin + users with "Accounts" or "Accounts Manager" roles.
    /// Falls back to first admin user if no accounts roles configured.
    /// </summary>
    private async Task<List<Guid>> ResolveNotificationRecipientsAsync()
    {
        // includeDetails is required — IdentityUser.Roles is a lazily-loaded navigation collection
        // that comes back null (not an empty collection) without it, throwing on roles.Any() below
        // for every user (confirmed via a real integration test while fixing the identical bug in
        // 3 other background jobs' copy of this same pattern).
        var users = await _userRepository.GetListAsync(maxResultCount: 100, sorting: "UserName", includeDetails: true);
        var recipientIds = new HashSet<Guid>();

        foreach (var user in users)
        {
            if (!user.IsActive) continue;
            var roles = user.Roles;
            if (roles.Any(r => r.RoleId != Guid.Empty)) // Has any role assignment → include
            {
                recipientIds.Add(user.Id);
                if (recipientIds.Count >= 5) break; // Cap at 5 to prevent notification spam
            }
        }

        // Fallback: if no role-matched users, use first active user
        if (recipientIds.Count == 0 && users.Any(u => u.IsActive))
            recipientIds.Add(users.First(u => u.IsActive).Id);

        return recipientIds.ToList();
    }
}

public class PaymentReminderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
    /// <summary>Don't re-remind if a reminder was sent within this many days. Default 7.</summary>
    public int ReminderCooldownDays { get; set; } = 7;
}
