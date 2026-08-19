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
/// Background job that monitors customer credit limit consumption and alerts credit control officers
/// of accounts exceeding or nearing credit limit thresholds.
/// Per ERPNext: customer.update_credit_limit_status (daily scheduler).
/// </summary>
public class CustomerCreditLimitSyncJob : AsyncBackgroundJob<CustomerCreditLimitSyncJobArgs>, ITransientDependency
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<CustomerCreditLimit, Guid> _creditLimitRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CustomerCreditLimitSyncJob> _logger;

    public CustomerCreditLimitSyncJob(
        IRepository<Customer, Guid> customerRepository,
        IRepository<CustomerCreditLimit, Guid> creditLimitRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<CustomerCreditLimitSyncJob> logger)
    {
        _customerRepository = customerRepository;
        _creditLimitRepository = creditLimitRepository;
        _invoiceRepository = invoiceRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(CustomerCreditLimitSyncJobArgs args)
    {
        _logger.LogInformation("CustomerCreditLimitSyncJob: Checking credit limit utilization for company {CompanyId}",
            args.CompanyId);

        var custQuery = await _customerRepository.GetQueryableAsync();
        var limitQuery = await _creditLimitRepository.GetQueryableAsync();
        var invQuery = await _invoiceRepository.GetQueryableAsync();

        var companyLimits = limitQuery
            .Where(l => l.CompanyId == args.CompanyId && l.CreditLimit > 0 && !l.BypassCreditLimitCheck)
            .ToList();

        var customers = custQuery
            .Where(c => c.CompanyId == args.CompanyId && (c.CreditLimit > 0 || companyLimits.Any(l => l.CustomerId == c.Id)))
            .ToList();

        if (!customers.Any())
            return;

        var postedInvoices = invQuery
            .Where(i => i.CompanyId == args.CompanyId &&
                        i.Status == DocumentStatus.Posted &&
                        i.OutstandingAmount > 0)
            .ToList();

        var breachedAccounts = 0;
        var alertItems = "";

        foreach (var customer in customers)
        {
            var effectiveLimit = companyLimits.FirstOrDefault(l => l.CustomerId == customer.Id)?.CreditLimit ?? customer.CreditLimit;
            if (effectiveLimit <= 0)
                continue;

            var totalOutstanding = postedInvoices
                .Where(i => i.CustomerId == customer.Id)
                .Sum(i => i.OutstandingAmount);

            var utilization = Math.Round((totalOutstanding / effectiveLimit) * 100m, 1);

            if (utilization >= 90m)
            {
                breachedAccounts++;
                alertItems += $"<li><strong>{customer.Name} ({customer.CustomerCode ?? "N/A"})</strong> - Outstanding: MYR {totalOutstanding:N2} / Limit: MYR {effectiveLimit:N2} ({utilization}% utilization)</li>";
            }
        }

        if (breachedAccounts > 0)
        {
            var usersQuery = await _userRepository.GetQueryableAsync();
            var creditOfficers = usersQuery
                .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
                .Take(5)
                .ToList();

            var subject = $"[CREDIT ALERT] {breachedAccounts} Customer Accounts Above 90% Credit Limit";
            var body = $@"<h3>Customer Credit Limit Threshold Alert</h3>
<p>There are {breachedAccounts} customer account(s) that have reached or exceeded 90% of their credit limit for company:</p>
<ul>
{alertItems}
</ul>
<p><em>Please review accounts before approving new Sales Orders or Delivery Notes in MyERP.</em></p>";

            foreach (var user in creditOfficers)
            {
                try
                {
                    await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CustomerCreditLimitSyncJob: Failed to send credit alert to {Email}", user.Email);
                }
            }
        }

        _logger.LogInformation("CustomerCreditLimitSyncJob: Processed {Total} customers ({Breached} near/over limit) for company {CompanyId}",
            customers.Count, breachedAccounts, args.CompanyId);
    }
}

public class CustomerCreditLimitSyncJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
