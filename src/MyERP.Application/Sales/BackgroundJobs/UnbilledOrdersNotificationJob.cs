using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that identifies and alerts management about delivered-but-unbilled Sales Orders and received-but-unbilled Purchase Orders.
/// Per ERPNext: selling/buying unbilled orders digest notification.
/// </summary>
public class UnbilledOrdersNotificationJob : AsyncBackgroundJob<UnbilledOrdersNotificationJobArgs>, ITransientDependency
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UnbilledOrdersNotificationJob> _logger;

    public UnbilledOrdersNotificationJob(
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<UnbilledOrdersNotificationJob> logger)
    {
        _salesOrderRepository = salesOrderRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _companyRepository = companyRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(UnbilledOrdersNotificationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("UnbilledOrdersNotificationJob: Checking unbilled orders for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var company = await _companyRepository.FindAsync(args.CompanyId);
        if (company == null) return;

        var soQuery = await _salesOrderRepository.WithDetailsAsync(so => so.Items);
        var unbilledSalesOrders = soQuery
            .Where(so => so.CompanyId == args.CompanyId &&
                         so.Status != DocumentStatus.Draft &&
                         so.Status != DocumentStatus.Cancelled &&
                         so.PerDelivered > so.PerBilled)
            .ToList();

        var poQuery = await _purchaseOrderRepository.WithDetailsAsync(po => po.Items);
        var unbilledPurchaseOrders = poQuery
            .Where(po => po.CompanyId == args.CompanyId &&
                         po.Status != DocumentStatus.Draft &&
                         po.Status != DocumentStatus.Cancelled &&
                         po.PerReceived > po.PerBilled)
            .ToList();

        if (!unbilledSalesOrders.Any() && !unbilledPurchaseOrders.Any())
        {
            _logger.LogInformation("UnbilledOrdersNotificationJob: No unbilled delivered/received orders for company {CompanyId}", args.CompanyId);
            return;
        }

        // Send summary to system admin users
        var adminQuery = await _userRepository.GetQueryableAsync();
        var adminUsers = adminQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"Unbilled Orders Digest: {company.Name} ({asOfDate:yyyy-MM-dd})";
        var body = $@"<h3>Unbilled Orders Digest</h3>
<p><strong>Company:</strong> {company.Name}</p>
<p><strong>Delivered but Unbilled Sales Orders:</strong> {unbilledSalesOrders.Count}</p>
<p><strong>Received but Unbilled Purchase Orders:</strong> {unbilledPurchaseOrders.Count}</p>
<hr/>
<h4>Sales Orders Requiring Invoicing:</h4>
<ul>
{string.Join("", unbilledSalesOrders.Take(10).Select(so => $"<li>{so.OrderNumber}: Delivered {so.PerDelivered:N0}%, Billed {so.PerBilled:N0}% (Total: {so.GrandTotal:N2} {so.CurrencyCode})</li>"))}
</ul>
<h4>Purchase Orders Requiring Supplier Invoicing:</h4>
<ul>
{string.Join("", unbilledPurchaseOrders.Take(10).Select(po => $"<li>{po.OrderNumber}: Received {po.PerReceived:N0}%, Billed {po.PerBilled:N0}% (Total: {po.GrandTotal:N2} {po.CurrencyCode})</li>"))}
</ul>";

        foreach (var admin in adminUsers)
        {
            try
            {
                await _emailSender.SendAsync(admin.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UnbilledOrdersNotificationJob: Failed to send digest email to {Email}", admin.Email);
            }
        }

        _logger.LogInformation("UnbilledOrdersNotificationJob: Sent unbilled orders digest for company {CompanyId} ({SOCount} SOs, {POCount} POs)",
            args.CompanyId, unbilledSalesOrders.Count, unbilledPurchaseOrders.Count);
    }
}

public class UnbilledOrdersNotificationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
