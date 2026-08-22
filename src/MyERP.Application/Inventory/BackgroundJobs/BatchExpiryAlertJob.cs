using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using MyERP.Notification;
using MyERP.Notification.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.Inventory.BackgroundJobs;

public class BatchExpiryAlertJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; }
    public int AlertDaysAhead { get; set; } = 30;
}

/// <summary>
/// Daily background job that alerts users about batches expiring within N days.
/// Per DO-NOT: "Implement batch expiry without blocking expired batch consumption in transactions"
/// This is the PROACTIVE alert complement to the transactional blocking validation.
/// Creates notifications grouped by urgency: expired (critical), expiring within 7 days (warning), 
/// expiring within N days (info).
/// </summary>
public class BatchExpiryAlertJob : AsyncBackgroundJob<BatchExpiryAlertJobArgs>, ITransientDependency
{
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly ILogger<BatchExpiryAlertJob> _logger;

    public BatchExpiryAlertJob(
        IRepository<Batch, Guid> batchRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<AppNotification, Guid> notificationRepository,
        IIdentityUserRepository userRepository,
        ILogger<BatchExpiryAlertJob> logger)
    {
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(BatchExpiryAlertJobArgs args)
    {
        _logger.LogInformation("BatchExpiryAlertJob: Checking expiring batches for company {CompanyId}, alertDays={Days}",
            args.CompanyId, args.AlertDaysAhead);

        var today = args.AsOfDate.Date;
        var alertCutoff = today.AddDays(args.AlertDaysAhead);

        var queryable = await _batchRepository.GetQueryableAsync();
        var expiringBatches = queryable
            .Where(b => !b.IsDisabled
                && b.ExpiryDate != null
                && b.ExpiryDate <= alertCutoff)
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        if (expiringBatches.Count == 0)
        {
            _logger.LogDebug("BatchExpiryAlertJob: No expiring batches for company {CompanyId}", args.CompanyId);
            return;
        }

        // Resolve item names for display
        var itemIds = expiringBatches.Select(b => b.ItemId).Distinct().ToList();
        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var itemNames = itemQueryable
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName })
            .ToDictionary(i => i.Id, i => i.ItemName ?? "Unknown");

        // Classify by urgency
        var expired = expiringBatches.Where(b => b.ExpiryDate!.Value.Date < today).ToList();
        var criticallyExpiring = expiringBatches.Where(b => b.ExpiryDate!.Value.Date >= today && b.ExpiryDate!.Value.Date <= today.AddDays(7)).ToList();
        var warningExpiring = expiringBatches.Where(b => b.ExpiryDate!.Value.Date > today.AddDays(7) && b.ExpiryDate!.Value.Date <= alertCutoff).ToList();

        // Resolve recipients (stock/warehouse role users)
        var targetUserIds = await ResolveRecipientsAsync();
        if (targetUserIds.Count == 0)
        {
            _logger.LogWarning("BatchExpiryAlertJob: No notification recipients found");
            return;
        }

        var notificationsCreated = 0;

        // Create CRITICAL alert for expired batches (immediate action needed)
        if (expired.Count > 0)
        {
            var subject = $"CRITICAL: {expired.Count} batch(es) have EXPIRED and must not be shipped";
            var body = BuildBatchListBody(expired, itemNames, "expired");

            foreach (var userId in targetUserIds)
            {
                await _notificationRepository.InsertAsync(new AppNotification(
                    Guid.NewGuid(), userId, subject)
                {
                    Body = body,
                    Severity = NotificationSeverity.Error,
                    ActionUrl = "/inventory/batches",
                    SourceDocumentType = "Batch",
                });
            }
            notificationsCreated++;
        }

        // Create WARNING alert for batches expiring within 7 days
        if (criticallyExpiring.Count > 0)
        {
            var subject = $"WARNING: {criticallyExpiring.Count} batch(es) expiring within 7 days";
            var body = BuildBatchListBody(criticallyExpiring, itemNames, "expiring soon");

            foreach (var userId in targetUserIds)
            {
                await _notificationRepository.InsertAsync(new AppNotification(
                    Guid.NewGuid(), userId, subject)
                {
                    Body = body,
                    Severity = NotificationSeverity.Warning,
                    ActionUrl = "/inventory/batches",
                    SourceDocumentType = "Batch",
                });
            }
            notificationsCreated++;
        }

        // Create INFO alert for batches expiring within N days (lower urgency)
        if (warningExpiring.Count > 0)
        {
            var subject = $"INFO: {warningExpiring.Count} batch(es) expiring within {args.AlertDaysAhead} days";
            var body = BuildBatchListBody(warningExpiring, itemNames, "expiring");

            foreach (var userId in targetUserIds)
            {
                await _notificationRepository.InsertAsync(new AppNotification(
                    Guid.NewGuid(), userId, subject)
                {
                    Body = body,
                    Severity = NotificationSeverity.Info,
                    ActionUrl = "/inventory/batches",
                    SourceDocumentType = "Batch",
                });
            }
            notificationsCreated++;
        }

        _logger.LogInformation(
            "BatchExpiryAlertJob: Company {CompanyId} — {Expired} expired, {Critical} critical, {Warning} warning batches. {NotifCount} notifications created.",
            args.CompanyId, expired.Count, criticallyExpiring.Count, warningExpiring.Count, notificationsCreated);
    }

    private static string BuildBatchListBody(List<Batch> batches, Dictionary<Guid, string> itemNames, string status)
    {
        var top5 = batches.Take(5);
        var lines = top5.Select(b =>
        {
            var itemName = itemNames.GetValueOrDefault(b.ItemId, "Unknown");
            var expiry = b.ExpiryDate?.ToString("yyyy-MM-dd") ?? "N/A";
            return $"• {b.BatchNo} ({itemName}) — {status}, expiry: {expiry}";
        });

        var body = string.Join("\n", lines);
        if (batches.Count > 5)
            body += $"\n... and {batches.Count - 5} more";

        return body;
    }

    private async Task<List<Guid>> ResolveRecipientsAsync()
    {
        // includeDetails is required — IdentityUser.Roles is a lazily-loaded navigation collection
        // that comes back null (not an empty collection) without it, throwing on .Roles.Any() below
        // for every user. Confirmed via a real integration test while fixing the same bug freshly
        // copied into 3 other background jobs (WorkOrderOverdueNotificationJob et al.) — this
        // original method had never actually been exercised end-to-end before that.
        var users = await _userRepository.GetListAsync(maxResultCount: 50, sorting: "UserName", includeDetails: true);
        return users
            .Where(u => u.IsActive && u.Roles.Any())
            .Take(5)
            .Select(u => u.Id)
            .ToList();
    }
}
