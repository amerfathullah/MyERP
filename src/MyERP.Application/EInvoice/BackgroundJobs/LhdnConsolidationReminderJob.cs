using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.EInvoice.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.EInvoice.BackgroundJobs;

/// <summary>
/// Background job that monitors unconsolidated B2C sales invoices for the previous month
/// and dispatches urgent reminder alerts within the 7-day monthly LHDN consolidation window.
/// Per Malaysia LHDN MyInvois e-Invoice compliance: B2C consolidation must be submitted by the 7th of each month.
/// </summary>
public class LhdnConsolidationReminderJob : AsyncBackgroundJob<LhdnConsolidationReminderJobArgs>, ITransientDependency
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<EInvoiceConsolidation, Guid> _consolidationRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<LhdnConsolidationReminderJob> _logger;

    public LhdnConsolidationReminderJob(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<EInvoiceConsolidation, Guid> consolidationRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Customer, Guid> customerRepository,
        IEmailSender emailSender,
        ILogger<LhdnConsolidationReminderJob> logger)
    {
        _invoiceRepository = invoiceRepository;
        _consolidationRepository = consolidationRepository;
        _companyRepository = companyRepository;
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(LhdnConsolidationReminderJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;

        // Only active during the first 7 days of the month (LHDN monthly consolidation window)
        if (asOfDate.Day > 7)
            return;

        var prevMonthStart = new DateTime(asOfDate.Year, asOfDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var prevMonthEnd = new DateTime(asOfDate.Year, asOfDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);

        _logger.LogInformation("LhdnConsolidationReminderJob: Checking unconsolidated B2C invoices for company {CompanyId} for {Period}",
            args.CompanyId, prevMonthStart.ToString("MMMM yyyy"));

        var company = await _companyRepository.FindAsync(args.CompanyId);
        if (company == null) return;

        var invQuery = await _invoiceRepository.GetQueryableAsync();
        var candidateInvoices = invQuery
            .Where(i => i.CompanyId == args.CompanyId &&
                        i.Status == DocumentStatus.Posted &&
                        // Already submitted individually (e.g. correctly as a B2B invoice, or
                        // manually) — nothing left to consolidate for this one.
                        i.EInvoiceStatus != EInvoiceStatus.Valid &&
                        i.IssueDate >= prevMonthStart &&
                        i.IssueDate <= prevMonthEnd)
            .ToList();

        // B2C only — the same "no real buyer TIN" rule GetConsolidationCandidatesAsync and
        // ConsolidateInvoicesAsync use. A B2B invoice belongs in this job's target set as much
        // as it belongs in an anonymous consolidated submission: not at all.
        var candidateCustomerIds = candidateInvoices.Select(i => i.CustomerId).Distinct().ToList();
        var candidateCustomerTins = (await _customerRepository.GetQueryableAsync())
            .Where(c => candidateCustomerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Tin);
        var b2cInvoices = candidateInvoices
            .Where(i => string.IsNullOrWhiteSpace(i.BuyerTin)
                     && (!candidateCustomerTins.TryGetValue(i.CustomerId, out var tin) || string.IsNullOrWhiteSpace(tin)))
            .ToList();

        if (!b2cInvoices.Any())
            return;

        var consQuery = await _consolidationRepository.GetQueryableAsync();
        var consolidatedIds = consQuery
            .Where(c => c.CompanyId == args.CompanyId)
            .Select(c => c.OriginalInvoiceId)
            .Distinct()
            .ToList();

        var unconsolidated = b2cInvoices
            .Where(i => !consolidatedIds.Contains(i.Id))
            .ToList();

        if (!unconsolidated.Any())
        {
            _logger.LogInformation("LhdnConsolidationReminderJob: All B2C invoices for {Period} are consolidated.",
                prevMonthStart.ToString("MMMM yyyy"));
            return;
        }

        var totalAmount = unconsolidated.Sum(i => i.GrandTotal);
        var daysRemaining = 7 - asOfDate.Day;

        var usersQuery = await _userRepository.GetQueryableAsync();
        var financeUsers = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"[ACTION REQUIRED - {daysRemaining} DAYS LEFT] LHDN Monthly B2C Consolidation Reminder ({company.Name})";
        var body = $@"<h3>LHDN Monthly Consolidated e-Invoice Reminder</h3>
<p><strong>Company:</strong> {company.Name}</p>
<p><strong>Target Period:</strong> {prevMonthStart:MMMM yyyy}</p>
<p><strong>Unconsolidated B2C Invoices:</strong> {unconsolidated.Count}</p>
<p><strong>Total Unconsolidated Amount:</strong> MYR {totalAmount:N2}</p>
<p><strong>Deadline:</strong> {asOfDate.AddDays(daysRemaining):yyyy-MM-07} (<strong>{daysRemaining} day(s) remaining</strong>)</p>
<hr/>
<p><em>Under LHDN guidelines, all B2C retail transactions must be consolidated and submitted via MyInvois within 7 days from the end of each calendar month.</em></p>";

        foreach (var user in financeUsers)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LhdnConsolidationReminderJob: Failed to send reminder email to {Email}", user.Email);
            }
        }

        _logger.LogInformation("LhdnConsolidationReminderJob: Sent LHDN consolidation reminder for {Count} invoices (MYR {Amount:N2}) for company {CompanyId}",
            unconsolidated.Count, totalAmount, args.CompanyId);
    }
}

public class LhdnConsolidationReminderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
