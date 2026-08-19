using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that monitors Blanket Orders nearing validity expiration or full quantity fulfillment.
/// Per ERPNext: blanket_order.update_blanket_order_status (daily scheduler).
/// </summary>
public class BlanketOrderExpiryJob : AsyncBackgroundJob<BlanketOrderExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<BlanketOrder, Guid> _blanketOrderRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<BlanketOrderExpiryJob> _logger;

    public BlanketOrderExpiryJob(
        IRepository<BlanketOrder, Guid> blanketOrderRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<BlanketOrderExpiryJob> logger)
    {
        _blanketOrderRepository = blanketOrderRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(BlanketOrderExpiryJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var upcomingExpiry = asOfDate.AddDays(14);

        _logger.LogInformation("BlanketOrderExpiryJob: Checking blanket order validity for company {CompanyId} up to {Date}",
            args.CompanyId, upcomingExpiry.ToString("yyyy-MM-dd"));

        var query = await _blanketOrderRepository.WithDetailsAsync(b => b.Items);
        var activeOrders = query
            .Where(b => b.CompanyId == args.CompanyId && b.Status == DocumentStatus.Submitted)
            .ToList();

        if (!activeOrders.Any())
            return;

        var expiringOrders = activeOrders
            .Where(b => b.ToDate <= upcomingExpiry || b.Items.All(i => i.RemainingQty <= 0))
            .ToList();

        if (!expiringOrders.Any())
            return;

        var usersQuery = await _userRepository.GetQueryableAsync();
        var salesOfficers = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"[BLANKET ORDER NOTICE] {expiringOrders.Count} Blanket Orders Expiring / Fully Ordered";
        var body = $@"<h3>Blanket Order Agreement Status Alert</h3>
<p>There are {expiringOrders.Count} blanket order agreement(s) expiring within 14 days or fully consumed:</p>
<ul>
{string.Join("", expiringOrders.Select(b => $"<li><strong>{b.OrderNumber} ({b.OrderType})</strong> - Party: {b.PartyName ?? b.PartyId.ToString()} | Validity: {b.FromDate:yyyy-MM-dd} to {b.ToDate:yyyy-MM-dd} | Items: {b.Items.Count}</li>"))}
</ul>
<p><em>Please renew framework contracts or close completed agreements in MyERP.</em></p>";

        foreach (var user in salesOfficers)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BlanketOrderExpiryJob: Failed to send blanket order alert to {Email}", user.Email);
            }
        }

        _logger.LogInformation("BlanketOrderExpiryJob: Sent alert for {Count} expiring/completed blanket orders for company {CompanyId}",
            expiringOrders.Count, args.CompanyId);
    }
}

public class BlanketOrderExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
