using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Manufacturing.Entities;
using MyERP.Notification;
using MyERP.Notification.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing.BackgroundJobs;

/// <summary>
/// Detects overdue Work Orders (planned end date passed while still active) and
/// creates notifications for production managers.
/// Per ERPNext: send_notification_for_overdue_work_orders daily scheduler.
/// 
/// Overdue = PlannedEndDate &lt; today AND status is NotStarted or InProcess.
/// Groups by severity: critical (&gt;7 days overdue), warning (1-7 days).
/// </summary>
public class WorkOrderOverdueNotificationJob : AsyncBackgroundJob<WorkOrderOverdueNotificationJobArgs>, ITransientDependency
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly ILogger<WorkOrderOverdueNotificationJob> _logger;

    public WorkOrderOverdueNotificationJob(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<AppNotification, Guid> notificationRepository,
        ILogger<WorkOrderOverdueNotificationJob> logger)
    {
        _workOrderRepository = workOrderRepository;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(WorkOrderOverdueNotificationJobArgs args)
    {
        _logger.LogInformation("WorkOrderOverdueNotificationJob: Checking company {CompanyId}", args.CompanyId);

        var today = args.AsOfDate.Date;
        var queryable = await _workOrderRepository.GetQueryableAsync();

        var overdueOrders = queryable
            .Where(wo => wo.CompanyId == args.CompanyId
                && wo.PlannedEndDate != null
                && wo.PlannedEndDate < today
                && (wo.Status == WorkOrderStatus.NotStarted || wo.Status == WorkOrderStatus.InProcess))
            .Select(wo => new
            {
                wo.Id,
                wo.WorkOrderNumber,
                wo.PlannedEndDate,
                wo.Status,
                wo.Quantity,
                wo.ProducedQuantity
            })
            .ToList();

        if (overdueOrders.Count == 0)
        {
            _logger.LogDebug("WorkOrderOverdueNotificationJob: No overdue orders for company {CompanyId}", args.CompanyId);
            return;
        }

        var criticalCount = overdueOrders.Count(o => (today - o.PlannedEndDate!.Value).TotalDays > 7);
        var warningCount = overdueOrders.Count - criticalCount;

        var severity = criticalCount > 0
            ? NotificationSeverity.Error
            : NotificationSeverity.Warning;

        var details = overdueOrders
            .OrderByDescending(o => (today - o.PlannedEndDate!.Value).TotalDays)
            .Take(5)
            .Select(o => $"{o.WorkOrderNumber}: {(int)(today - o.PlannedEndDate!.Value).TotalDays}d overdue ({o.ProducedQuantity}/{o.Quantity})")
            .ToList();

        var message = $"{overdueOrders.Count} overdue Work Order(s) ({criticalCount} critical, {warningCount} warning). Top: {string.Join("; ", details)}";

        var notification = new AppNotification(
            Guid.NewGuid(),
            args.UserId,
            message,
            args.TenantId)
        {
            Severity = severity,
            SourceDocumentType = "WorkOrder"
        };

        await _notificationRepository.InsertAsync(notification);

        _logger.LogInformation(
            "WorkOrderOverdueNotificationJob: {Count} overdue WOs found for company {CompanyId} ({Critical} critical)",
            overdueOrders.Count, args.CompanyId, criticalCount);
    }
}

public class WorkOrderOverdueNotificationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
    public Guid UserId { get; set; }
}
