using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Projects.BackgroundJobs;

/// <summary>
/// Background job that monitors unbilled billable timesheets and alerts project managers.
/// Per ERPNext: timesheet.update_billing_status (daily scheduler).
/// </summary>
public class TimesheetBillingStatusJob : AsyncBackgroundJob<TimesheetBillingStatusJobArgs>, ITransientDependency
{
    private readonly IRepository<Timesheet, Guid> _timesheetRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<TimesheetBillingStatusJob> _logger;

    public TimesheetBillingStatusJob(
        IRepository<Timesheet, Guid> timesheetRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<TimesheetBillingStatusJob> logger)
    {
        _timesheetRepository = timesheetRepository;
        _invoiceRepository = invoiceRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(TimesheetBillingStatusJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var unbilledThreshold = asOfDate.AddDays(-14);

        _logger.LogInformation("TimesheetBillingStatusJob: Checking unbilled timesheets for company {CompanyId} up to {Date}",
            args.CompanyId, unbilledThreshold.ToString("yyyy-MM-dd"));

        var query = await _timesheetRepository.WithDetailsAsync(t => t.Details);
        var submittedTimesheets = query
            .Where(t => t.CompanyId == args.CompanyId &&
                        t.Status == TimesheetStatus.Submitted &&
                        t.EndDate <= unbilledThreshold)
            .ToList();

        if (!submittedTimesheets.Any())
            return;

        var unbilledTimesheets = submittedTimesheets
            .Where(t => t.Details.Any(d => d.IsBillable && d.SalesInvoiceId == null))
            .ToList();

        if (!unbilledTimesheets.Any())
            return;

        var totalUnbilledHours = unbilledTimesheets.Sum(t => t.Details.Where(d => d.IsBillable && d.SalesInvoiceId == null).Sum(d => d.Hours));
        var totalUnbilledAmount = unbilledTimesheets.Sum(t => t.Details.Where(d => d.IsBillable && d.SalesInvoiceId == null).Sum(d => d.BillingAmount));

        var usersQuery = await _userRepository.GetQueryableAsync();
        var projectManagers = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"[BILLING NOTICE] {unbilledTimesheets.Count} Submitted Timesheets Pending Invoicing ({totalUnbilledHours:N1} hrs / MYR {totalUnbilledAmount:N2})";
        var body = $@"<h3>Unbilled Project Timesheets Alert</h3>
<p>There are {unbilledTimesheets.Count} submitted timesheet(s) with unbilled billable hours older than 14 days:</p>
<ul>
    <li><strong>Total Unbilled Hours:</strong> {totalUnbilledHours:N1} hrs</li>
    <li><strong>Total Billable Value:</strong> MYR {totalUnbilledAmount:N2}</li>
</ul>
<p><em>Please generate Sales Invoices from project timesheets in MyERP.</em></p>";

        foreach (var user in projectManagers)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TimesheetBillingStatusJob: Failed to send billing reminder to {Email}", user.Email);
            }
        }

        _logger.LogInformation("TimesheetBillingStatusJob: Sent reminder for {Count} unbilled timesheets for company {CompanyId} (Total: MYR {Total:N2})",
            unbilledTimesheets.Count, totalUnbilledAmount, args.CompanyId);
    }
}

public class TimesheetBillingStatusJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
