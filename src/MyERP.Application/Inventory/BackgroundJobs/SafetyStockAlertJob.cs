using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using MyERP.Notification.DomainServices;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.Inventory.BackgroundJobs;

public class SafetyStockAlertJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}

/// <summary>
/// Daily background job that alerts warehouse managers when an item's physical stock on hand has
/// fallen below its configured Safety Stock level — a stricter, "must act now" floor distinct from
/// ReorderLevel (which AutoReorderService already handles by auto-creating a Material Request).
/// Per Item.SafetyStock's own doc comment: "buffer kept above reorder level."
/// </summary>
/// <remarks>
/// BusinessNotificationService.NotifyLowStockAsync already existed for exactly this but had zero
/// callers anywhere (confirmed via grep while surveying the Notification module for unwired
/// methods) — Item.SafetyStock was set but nothing ever compared current stock against it.
/// </remarks>
public class SafetyStockAlertJob : AsyncBackgroundJob<SafetyStockAlertJobArgs>, ITransientDependency
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly BusinessNotificationService _notificationService;
    private readonly ILogger<SafetyStockAlertJob> _logger;

    public SafetyStockAlertJob(
        IRepository<Item, Guid> itemRepository,
        IRepository<Bin, Guid> binRepository,
        IIdentityUserRepository userRepository,
        BusinessNotificationService notificationService,
        ILogger<SafetyStockAlertJob> logger)
    {
        _itemRepository = itemRepository;
        _binRepository = binRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SafetyStockAlertJobArgs args)
    {
        _logger.LogInformation("SafetyStockAlertJob: Checking company {CompanyId}", args.CompanyId);

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var candidateItems = itemQuery
            .Where(i => i.CompanyId == args.CompanyId && i.IsActive && i.MaintainStock && i.SafetyStock > 0)
            .ToList();

        if (candidateItems.Count == 0)
        {
            _logger.LogDebug("SafetyStockAlertJob: No items with a Safety Stock level for company {CompanyId}", args.CompanyId);
            return;
        }

        var binQuery = await _binRepository.GetQueryableAsync();
        var itemIds = candidateItems.Select(i => i.Id).ToList();
        var actualQtyByItem = binQuery
            .Where(b => itemIds.Contains(b.ItemId))
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, ActualQty = g.Sum(b => b.ActualQty) })
            .ToDictionary(x => x.ItemId, x => x.ActualQty);

        var belowSafetyStock = candidateItems
            .Select(i => new { Item = i, ActualQty = actualQtyByItem.GetValueOrDefault(i.Id, 0m) })
            .Where(x => x.ActualQty < x.Item.SafetyStock)
            .ToList();

        if (belowSafetyStock.Count == 0)
        {
            _logger.LogDebug("SafetyStockAlertJob: All items at/above Safety Stock for company {CompanyId}", args.CompanyId);
            return;
        }

        var targetUserIds = await ResolveRecipientsAsync();
        if (targetUserIds.Count == 0)
        {
            _logger.LogWarning("SafetyStockAlertJob: No notification recipients found for company {CompanyId}", args.CompanyId);
            return;
        }

        foreach (var low in belowSafetyStock)
        {
            foreach (var userId in targetUserIds)
            {
                await _notificationService.NotifyLowStockAsync(
                    userId, low.Item.ItemName, low.ActualQty, low.Item.SafetyStock, args.TenantId);
            }
        }

        _logger.LogInformation(
            "SafetyStockAlertJob: {Count} item(s) below Safety Stock for company {CompanyId}, {RecipientCount} recipients notified",
            belowSafetyStock.Count, args.CompanyId, targetUserIds.Count);
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
