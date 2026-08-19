using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.CRM.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM.BackgroundJobs;

/// <summary>
/// Background job that reopens lost/closed leads and opportunities that have appointments/events scheduled for today.
/// Per ERPNext: open_leads_opportunities_based_on_todays_event (daily scheduler).
/// </summary>
public class CrmEventReopenJob : AsyncBackgroundJob<CrmEventReopenJobArgs>, ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly ILogger<CrmEventReopenJob> _logger;

    public CrmEventReopenJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Lead, Guid> leadRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        ILogger<CrmEventReopenJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(CrmEventReopenJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var startOfDay = asOfDate.Date;
        var endOfDay = startOfDay.AddDays(1);

        _logger.LogInformation("CrmEventReopenJob: Checking scheduled appointments for company {CompanyId} on {Date}",
            args.CompanyId, asOfDate);

        var apptQuery = await _appointmentRepository.GetQueryableAsync();
        var todaysAppointments = apptQuery
            .Where(a => a.CompanyId == args.CompanyId &&
                        a.Status == AppointmentStatus.Open &&
                        a.ScheduledTime >= startOfDay &&
                        a.ScheduledTime < endOfDay &&
                        a.PartyId.HasValue)
            .ToList();

        if (!todaysAppointments.Any())
            return;

        var leadIds = todaysAppointments
            .Where(a => string.Equals(a.PartyType, "Lead", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.PartyId!.Value)
            .Distinct()
            .ToList();

        var oppIds = todaysAppointments
            .Where(a => string.Equals(a.PartyType, "Opportunity", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.PartyId!.Value)
            .Distinct()
            .ToList();

        var reopenedLeads = 0;
        foreach (var leadId in leadIds)
        {
            var lead = await _leadRepository.FindAsync(leadId);
            if (lead != null && (lead.Status == LeadStatus.Lost || lead.Status == LeadStatus.DoNotContact))
            {
                lead.Reopen();
                await _leadRepository.UpdateAsync(lead);
                reopenedLeads++;
            }
        }

        var reopenedOpps = 0;
        foreach (var oppId in oppIds)
        {
            var opp = await _opportunityRepository.FindAsync(oppId);
            if (opp != null && (opp.Status == OpportunityStatus.Lost || opp.Status == OpportunityStatus.Closed))
            {
                opp.Reopen();
                await _opportunityRepository.UpdateAsync(opp);
                reopenedOpps++;
            }
        }

        _logger.LogInformation("CrmEventReopenJob: Reopened {LeadCount} leads and {OppCount} opportunities for company {CompanyId}",
            reopenedLeads, reopenedOpps, args.CompanyId);
    }
}

public class CrmEventReopenJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
