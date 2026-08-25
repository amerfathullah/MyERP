using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Notification;
using MyERP.Notification.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Creates in-app notifications for procurement staff when stock falls below reorder level.
/// Per ERPNext reorder_item.py: groups low-stock items per company and notifies designated users.
/// </summary>
public class StockAlertNotificationService : DomainService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly IIdentityUserRepository _userRepository;

    public StockAlertNotificationService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<AppNotification, Guid> notificationRepository,
        IIdentityUserRepository userRepository)
    {
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Checks if a specific item is below reorder level and creates a notification if so.
    /// Called after stock-out events (DN submit, SI UpdateStock, SE Issue/Transfer).
    /// </summary>
    public async Task CheckAndNotifyAsync(
        Guid itemId, Guid warehouseId, Guid companyId, Guid? tenantId = null)
    {
        var recipients = await ResolveRecipientsAsync();
        if (recipients.Count == 0) return;

        await CheckAndNotifyCoreAsync(itemId, warehouseId, recipients, tenantId);
    }

    /// <summary>
    /// Batch check for multiple items at once (e.g., after Stock Entry post). Resolves recipients
    /// once up front rather than per item.
    /// Per ERPNext: groups notifications by company, creates one per low-stock item.
    /// </summary>
    public async Task CheckMultipleAndNotifyAsync(
        IEnumerable<Guid> itemIds, Guid warehouseId, Guid companyId, Guid? tenantId = null)
    {
        var recipients = await ResolveRecipientsAsync();
        if (recipients.Count == 0) return;

        foreach (var itemId in itemIds.Distinct())
        {
            try
            {
                await CheckAndNotifyCoreAsync(itemId, warehouseId, recipients, tenantId);
            }
            catch
            {
                // Per ERPNext: per-item error isolation — one failure doesn't block others
            }
        }
    }

    private async Task CheckAndNotifyCoreAsync(
        Guid itemId, Guid warehouseId, List<Guid> recipients, Guid? tenantId)
    {
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var item = itemQuery.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.ReorderLevel <= 0) return;

        var binQuery = await _binRepository.GetQueryableAsync();
        var bin = binQuery.FirstOrDefault(b =>
            b.ItemId == itemId && b.WarehouseId == warehouseId);

        if (bin == null) return;

        var projected = bin.ProjectedQty;
        if (projected > item.ReorderLevel) return;

        foreach (var userId in recipients)
        {
            var notification = new AppNotification(
                Guid.NewGuid(),
                userId,
                $"Low Stock: {item.ItemCode}",
                tenantId)
            {
                Body = $"Stock for '{item.ItemName}' ({item.ItemCode}) is below reorder level. " +
                       $"Current projected: {projected:N2}, Reorder level: {item.ReorderLevel:N2}.",
                Severity = NotificationSeverity.Warning,
                ActionUrl = "/inventory/items/" + itemId,
            };

            await _notificationRepository.InsertAsync(notification);
        }
    }

    /// <summary>Same convention as SafetyStockAlertJob/BatchExpiryAlertJob.ResolveRecipientsAsync —
    /// active users with at least one role, capped at 5.</summary>
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
