using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that monitors submitted Payment Orders and alerts treasury officers of pending bank execution.
/// Per ERPNext: payment_order.update_payment_order_status (daily scheduler).
/// </summary>
public class PaymentOrderNotificationJob : AsyncBackgroundJob<PaymentOrderNotificationJobArgs>, ITransientDependency
{
    private readonly IRepository<PaymentOrder, Guid> _orderRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PaymentOrderNotificationJob> _logger;

    public PaymentOrderNotificationJob(
        IRepository<PaymentOrder, Guid> orderRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<PaymentOrderNotificationJob> logger)
    {
        _orderRepository = orderRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(PaymentOrderNotificationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("PaymentOrderNotificationJob: Checking pending payment orders for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _orderRepository.WithDetailsAsync(p => p.References);
        var pendingOrders = query
            .Where(p => p.CompanyId == args.CompanyId &&
                        p.Status == DocumentStatus.Submitted &&
                        p.PostingDate <= asOfDate)
            .ToList();

        if (!pendingOrders.Any())
            return;

        var usersQuery = await _userRepository.GetQueryableAsync();
        var financeUsers = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var totalAmount = pendingOrders.Sum(p => p.References.Sum(r => r.Amount));
        var subject = $"[ACTION REQUIRED] {pendingOrders.Count} Submitted Payment Orders Due For Execution (MYR {totalAmount:N2})";
        var body = $@"<h3>Pending Payment Orders Execution Alert</h3>
<p>There are {pendingOrders.Count} submitted payment order batch(es) due for bank transfer/file export totaling <strong>MYR {totalAmount:N2}</strong>:</p>
<ul>
{string.Join("", pendingOrders.Select(p => $"<li><strong>Order: {p.OrderNumber ?? p.Id.ToString()}</strong> - Type: {p.PaymentOrderType} | Posting Date: {p.PostingDate:yyyy-MM-dd} | Items: {p.References.Count} | Total: MYR {p.References.Sum(r => r.Amount):N2}</li>"))}
</ul>
<p><em>Please export bank payment files and disburse funds in MyERP.</em></p>";

        foreach (var user in financeUsers)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PaymentOrderNotificationJob: Failed to send payment order alert email to {Email}", user.Email);
            }
        }

        _logger.LogInformation("PaymentOrderNotificationJob: Sent alert for {Count} pending payment orders for company {CompanyId}",
            pendingOrders.Count, args.CompanyId);
    }
}

public class PaymentOrderNotificationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
