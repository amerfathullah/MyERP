using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.HumanResources.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.HumanResources.BackgroundJobs;

/// <summary>
/// Background job that monitors monthly payroll statutory deductions and reminds HR/Finance
/// of upcoming statutory remittance deadlines (EPF, SOCSO, EIS, PCB due by 15th of the month).
/// Per Malaysia compliance & statutory regulations.
/// </summary>
public class PayrollStatutoryRemittanceReminderJob : AsyncBackgroundJob<PayrollStatutoryRemittanceReminderJobArgs>, ITransientDependency
{
    private readonly IRepository<SalarySlip, Guid> _salarySlipRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PayrollStatutoryRemittanceReminderJob> _logger;

    public PayrollStatutoryRemittanceReminderJob(
        IRepository<SalarySlip, Guid> salarySlipRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<PayrollStatutoryRemittanceReminderJob> logger)
    {
        _salarySlipRepository = salarySlipRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(PayrollStatutoryRemittanceReminderJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;

        // Reminder active during 1st - 15th day of month
        if (asOfDate.Day > 15)
        {
            _logger.LogDebug("PayrollStatutoryRemittanceReminderJob: Past 15th deadline for month {Month}. Skipping reminder.", asOfDate.Month);
            return;
        }

        var prevMonth = asOfDate.AddMonths(-1);
        var prevMonthStart = new DateTime(prevMonth.Year, prevMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthEnd = new DateTime(asOfDate.Year, asOfDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1);

        _logger.LogInformation("PayrollStatutoryRemittanceReminderJob: Checking statutory deductions for company {CompanyId} for payroll period {Period}",
            args.CompanyId, prevMonthStart.ToString("yyyy-MM"));

        var query = await _salarySlipRepository.WithDetailsAsync(s => s.Deductions);
        var slips = query
            .Where(s => s.CompanyId == args.CompanyId &&
                        s.Status == DocumentStatus.Submitted &&
                        s.StartDate >= prevMonthStart &&
                        s.EndDate <= prevMonthEnd)
            .ToList();

        if (!slips.Any())
            return;

        var allDeductions = slips.SelectMany(s => s.Deductions).ToList();
        var statutoryDeductions = allDeductions.Where(d => d.IsStatutory).ToList();
        var totalStatutory = statutoryDeductions.Sum(d => d.Amount);

        if (totalStatutory <= 0 && !allDeductions.Any())
            return;

        var daysRemaining = 15 - asOfDate.Day;
        var usersQuery = await _userRepository.GetQueryableAsync();
        var payrollUsers = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"[URGENT REMINDER] Malaysia Statutory Remittance Due in {daysRemaining} Day(s) (MYR {totalStatutory:N2})";
        var body = $@"<h3>Malaysia Monthly Statutory Remittance Deadline Alert</h3>
<p>This is a reminder that statutory deductions for payroll period <strong>{prevMonthStart:yyyy-MM}</strong> must be remitted by the <strong>15th of this month</strong>.</p>
<ul>
    <li><strong>Total Payslips Processed:</strong> {slips.Count}</li>
    <li><strong>Total Statutory Deductions:</strong> MYR {totalStatutory:N2}</li>
    <li><strong>Statutory Components Breakdown:</strong></li>
    <ul>
        {string.Join("", statutoryDeductions.GroupBy(d => d.ComponentName).Select(g => $"<li>{g.Key}: MYR {g.Sum(x => x.Amount):N2}</li>"))}
    </ul>
    <li><strong>Days Remaining:</strong> {daysRemaining} day(s)</li>
</ul>
<p><em>Please ensure EPF (i-Akaun), SOCSO (ASSIST), EIS, and PCB (e-Data PCB / CP39) payments are finalized before the 15th deadline to avoid late penalties.</em></p>";

        foreach (var user in payrollUsers)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PayrollStatutoryRemittanceReminderJob: Failed to send statutory reminder to {Email}", user.Email);
            }
        }

        _logger.LogInformation("PayrollStatutoryRemittanceReminderJob: Sent statutory remittance reminder for company {CompanyId} (Total: MYR {Total:N2})",
            args.CompanyId, totalStatutory);
    }
}

public class PayrollStatutoryRemittanceReminderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
