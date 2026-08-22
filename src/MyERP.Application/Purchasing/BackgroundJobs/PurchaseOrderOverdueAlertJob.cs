using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Notification;
using MyERP.Notification.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.Purchasing.BackgroundJobs;

/// <summary>
/// Detects overdue Purchase Orders (expected delivery date passed while not fully received)
/// and creates notifications for procurement managers.
/// Per ERPNext: daily scheduler checks PO expected delivery date for follow-up.
///
/// Overdue = ExpectedDeliveryDate &lt; today AND status is ToDeliverAndBill or ToDeliver.
/// Groups by severity: critical (&gt;7 days overdue), warning (1-7 days).
/// </summary>
public class PurchaseOrderOverdueAlertJob : AsyncBackgroundJob<PurchaseOrderOverdueAlertJobArgs>, ITransientDependency
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly ILogger<PurchaseOrderOverdueAlertJob> _logger;

    public PurchaseOrderOverdueAlertJob(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<AppNotification, Guid> notificationRepository,
        IIdentityUserRepository userRepository,
        ILogger<PurchaseOrderOverdueAlertJob> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(PurchaseOrderOverdueAlertJobArgs args)
    {
        _logger.LogInformation("PurchaseOrderOverdueAlertJob: Checking company {CompanyId}", args.CompanyId);

        var today = args.AsOfDate.Date;
        var queryable = await _purchaseOrderRepository.GetQueryableAsync();

        var overdueOrders = queryable
            .Where(po => po.CompanyId == args.CompanyId
                && po.ExpectedDeliveryDate != null
                && po.ExpectedDeliveryDate < today
                && (po.Status == DocumentStatus.ToDeliverAndBill || po.Status == DocumentStatus.ToDeliver))
            .Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.ExpectedDeliveryDate,
                po.GrandTotal,
                po.Status
            })
            .ToList();

        if (overdueOrders.Count == 0)
        {
            _logger.LogDebug("PurchaseOrderOverdueAlertJob: No overdue POs for company {CompanyId}", args.CompanyId);
            return;
        }

        var criticalCount = overdueOrders.Count(o => (today - o.ExpectedDeliveryDate!.Value).TotalDays > 7);
        var warningCount = overdueOrders.Count - criticalCount;
        var totalValue = overdueOrders.Sum(o => o.GrandTotal);

        var severity = criticalCount > 0
            ? NotificationSeverity.Error
            : NotificationSeverity.Warning;

        var details = overdueOrders
            .OrderByDescending(o => (today - o.ExpectedDeliveryDate!.Value).TotalDays)
            .Take(5)
            .Select(o => $"{o.OrderNumber}: {(int)(today - o.ExpectedDeliveryDate!.Value).TotalDays}d overdue ({o.GrandTotal:N2})")
            .ToList();

        var message = $"{overdueOrders.Count} overdue Purchase Order(s) ({criticalCount} critical, {warningCount} warning), total value {totalValue:N2}. Top: {string.Join("; ", details)}";

        var targetUserIds = await ResolveRecipientsAsync();
        if (targetUserIds.Count == 0)
        {
            _logger.LogWarning("PurchaseOrderOverdueAlertJob: No notification recipients found for company {CompanyId}", args.CompanyId);
            return;
        }

        foreach (var userId in targetUserIds)
        {
            await _notificationRepository.InsertAsync(new AppNotification(
                Guid.NewGuid(), userId, message, args.TenantId)
            {
                Severity = severity,
                SourceDocumentType = "PurchaseOrder"
            });
        }

        _logger.LogInformation(
            "PurchaseOrderOverdueAlertJob: {Count} overdue POs for company {CompanyId} ({Critical} critical, value {Value:N2}), {RecipientCount} recipients notified",
            overdueOrders.Count, args.CompanyId, criticalCount, totalValue, targetUserIds.Count);
    }

    /// <summary>Same convention as BatchExpiryAlertJob.ResolveRecipientsAsync — active users with at
    /// least one role, capped at 5.</summary>
    private async Task<List<Guid>> ResolveRecipientsAsync()
    {
        var users = await _userRepository.GetListAsync(maxResultCount: 50, sorting: "UserName", includeDetails: true);
        return users
            .Where(u => u.IsActive && u.Roles.Any())
            .Take(5)
            .Select(u => u.Id)
            .ToList();
    }
}

public class PurchaseOrderOverdueAlertJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
}
