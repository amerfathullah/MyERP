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
/// Cancels expired unverified appointments past verification token expiry.
/// ERPNext equivalent: crm/doctype/appointment/appointment.py handle_expired_unverified_appointments (PR #57270).
/// </summary>
public class AppointmentExpiryJob : AsyncBackgroundJob<AppointmentExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _repository;
    private readonly ILogger<AppointmentExpiryJob> _logger;

    public AppointmentExpiryJob(IRepository<Appointment, Guid> repository, ILogger<AppointmentExpiryJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(AppointmentExpiryJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow;

        var query = await _repository.GetQueryableAsync();
        var expiredAppointments = query
            .Where(a => a.CompanyId == args.CompanyId &&
                        a.Status == AppointmentStatus.Unverified &&
                        a.VerificationTokenExpiresOn.HasValue &&
                        a.VerificationTokenExpiresOn.Value < asOfDate)
            .ToList();

        var cancelledCount = 0;
        foreach (var appt in expiredAppointments)
        {
            appt.CancelExpiredUnverified(asOfDate);
            await _repository.UpdateAsync(appt);
            cancelledCount++;
        }

        _logger.LogInformation("AppointmentExpiryJob cancelled {Count} expired unverified appointments for company {CompanyId} as of {Date}",
            cancelledCount, args.CompanyId, asOfDate);
    }
}

public class AppointmentExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
