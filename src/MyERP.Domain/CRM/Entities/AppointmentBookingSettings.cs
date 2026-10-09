using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Appointment Booking Settings — singleton per-company configuration for the customer-facing
/// appointment portal. Maps to ERPNext crm/doctype/appointment_booking_settings.
/// </summary>
public class AppointmentBookingSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public int AppointmentDurationMinutes { get; set; } = 60;
    public bool EnableScheduling { get; set; }
    public bool EnableAppointmentPortal { get; set; }
    public Guid? HolidayListId { get; set; }
    public int AdvanceBookingDays { get; set; } = 30;

    /// <summary>Minutes an unverified appointment's verification link stays valid (15-60).</summary>
    public int VerificationLinkExpiryMinutes { get; set; } = 15;
    public ExpiredAppointmentAction ActionForExpiredUnverified { get; set; } = ExpiredAppointmentAction.CancelAppointment;

    /// <summary>Comma-separated agent user IDs — stored flat rather than as a child table since it's a
    /// simple unordered set with no per-row data. Use <see cref="AgentUserIds"/>/<see cref="SetAgents"/>.</summary>
    public string? AgentUserIdsCsv { get; private set; }

    public IReadOnlyList<Guid> AgentUserIds =>
        string.IsNullOrEmpty(AgentUserIdsCsv)
            ? Array.Empty<Guid>()
            : AgentUserIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    public int NumberOfAgents => AgentUserIds.Count;

    private readonly List<AppointmentAvailability> _availability = new();
    public IReadOnlyList<AppointmentAvailability> AvailabilityOfSlots => _availability.AsReadOnly();

    protected AppointmentBookingSettings() { }

    public AppointmentBookingSettings(Guid id, Guid companyId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        TenantId = tenantId;
    }

    public void SetAgents(IEnumerable<Guid> agentUserIds)
    {
        AgentUserIdsCsv = string.Join(',', agentUserIds.Distinct());
    }

    public void SetAppointmentDurationMinutes(int minutes)
    {
        if (minutes <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Appointment Duration must be greater than 0 minutes.");
        }
        AppointmentDurationMinutes = minutes;
    }

    public void AddAvailability(AppointmentAvailability availability)
    {
        if (availability.FromTime >= availability.ToTime)
            throw new ArgumentException("FromTime must be before ToTime.", nameof(availability));
        _availability.Add(availability);
    }

    /// <summary>
    /// Validates that no availability slots on the same day of week overlap.
    /// Per ERPNext PR #59914 / commit dd78e6c0ed: refuse overlapping availability slots.
    /// </summary>
    public void ValidateAvailabilitySlots()
    {
        var slots = _availability.OrderBy(s => s.DayOfWeek).ThenBy(s => s.FromTime).ToList();
        for (int i = 1; i < slots.Count; i++)
        {
            var prev = slots[i - 1];
            var curr = slots[i];
            if (prev.DayOfWeek == curr.DayOfWeek && curr.FromTime < prev.ToTime)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Availability slots for {curr.DayOfWeek} cannot overlap: {prev.FromTime:hh\\:mm}-{prev.ToTime:hh\\:mm} and {curr.FromTime:hh\\:mm}-{curr.ToTime:hh\\:mm}.");
            }
        }
    }

    public void ClearAvailability()
    {
        _availability.Clear();
    }

    public void SetVerificationLinkExpiryMinutes(int minutes)
    {
        if (minutes < AppointmentBookingSettingsConsts.MinVerificationLinkExpiryMinutes
            || minutes > AppointmentBookingSettingsConsts.MaxVerificationLinkExpiryMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes),
                $"Verification link expiry must be between {AppointmentBookingSettingsConsts.MinVerificationLinkExpiryMinutes} and {AppointmentBookingSettingsConsts.MaxVerificationLinkExpiryMinutes} minutes.");
        }
        VerificationLinkExpiryMinutes = minutes;
    }

    /// <summary>Whether <paramref name="dayOfWeek"/> has an open booking window covering <paramref name="time"/>.</summary>
    public bool IsWithinServiceWindow(DayOfWeek dayOfWeek, TimeSpan time)
    {
        foreach (var window in _availability)
        {
            if (window.DayOfWeek == dayOfWeek && time >= window.FromTime && time <= window.ToTime)
                return true;
        }
        return false;
    }
}

/// <summary>One weekly booking window (e.g. Monday 09:00-17:00) for the appointment portal.</summary>
public class AppointmentAvailability : Entity<Guid>
{
    public Guid AppointmentBookingSettingsId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }

    protected AppointmentAvailability() { }

    public AppointmentAvailability(Guid id, Guid settingsId, DayOfWeek dayOfWeek, TimeSpan fromTime, TimeSpan toTime)
        : base(id)
    {
        AppointmentBookingSettingsId = settingsId;
        DayOfWeek = dayOfWeek;
        FromTime = fromTime;
        ToTime = toTime;
    }
}
