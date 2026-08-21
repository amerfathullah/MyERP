using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Support.Entities;

/// <summary>
/// Service Level Agreement — defines response/resolution targets by priority, scoped to a
/// customer/customer group/territory (EntityType+EntityId) or applied as the company default.
/// Maps to ERPNext support/doctype/service_level_agreement.
/// </summary>
public class ServiceLevelAgreement : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Targeting scope: "Customer", "Customer Group", "Territory". Null when IsDefault.</summary>
    public string? EntityType { get; set; }

    /// <summary>Id of the targeted Customer/Customer Group/Territory.</summary>
    public Guid? EntityId { get; set; }

    public Guid? CustomerGroupId { get; set; }
    public Guid? HolidayListId { get; set; }

    /// <summary>Fallback resolution target (hours) when no per-priority row matches.</summary>
    public int ResolutionTimeHours { get; set; }

    /// <summary>Fallback response target (hours) when no per-priority row matches.</summary>
    public int ResponseTimeHours { get; set; }

    /// <summary>Applied when a matching Customer/Group/Territory-specific SLA is not found.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Pause the resolution clock while the Issue is on hold.</summary>
    public bool ApplyOnResolution { get; set; } = true;

    public bool IsActive { get; set; } = true;

    private readonly List<ServiceLevelPriority> _priorities = new();
    public IReadOnlyList<ServiceLevelPriority> Priorities => _priorities.AsReadOnly();

    private readonly List<ServiceDay> _serviceDays = new();
    public IReadOnlyList<ServiceDay> ServiceDays => _serviceDays.AsReadOnly();

    protected ServiceLevelAgreement() { }

    public ServiceLevelAgreement(Guid id, Guid companyId, string name, int resolutionTimeHours, int responseTimeHours, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), ServiceLevelAgreementConsts.MaxNameLength);
        ResolutionTimeHours = resolutionTimeHours;
        ResponseTimeHours = responseTimeHours;
        TenantId = tenantId;
    }

    public void AddPriority(ServiceLevelPriority priority)
    {
        priority.Validate(ApplyOnResolution);
        _priorities.Add(priority);
    }

    /// <summary>
    /// Validates all priorities in this SLA (gotcha #827).
    /// Enforces: response_time must be positive; if apply_on_resolution enabled, resolution_time must be >= response_time.
    /// </summary>
    public void ValidatePriorities()
    {
        if (ApplyOnResolution && ResolutionTimeHours > 0 && ResolutionTimeHours < ResponseTimeHours)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Default Resolution Time must be greater than or equal to Response Time.");
        }

        foreach (var p in _priorities)
        {
            p.Validate(ApplyOnResolution);
        }
    }

    public void AddServiceDay(ServiceDay day)
    {
        _serviceDays.Add(day);
    }

    /// <summary>Per-priority targets, falling back to the SLA-level defaults when unmatched.</summary>
    public (decimal ResponseHours, decimal ResolutionHours) GetTargets(string? priorityName)
    {
        var match = string.IsNullOrWhiteSpace(priorityName)
            ? null
            : _priorities.FirstOrDefault(p => string.Equals(p.PriorityName, priorityName, StringComparison.OrdinalIgnoreCase));

        if (match != null)
            return (match.ResponseTimeHours, match.ResolutionTimeHours);

        return (ResponseTimeHours, ResolutionTimeHours);
    }

    /// <summary>
    /// Computes a working-hours-aware deadline. Walks forward from <paramref name="from"/>,
    /// counting only time inside configured ServiceDays windows; falls back to 24/7 if none configured.
    /// Mirrors ERPNext's get_expected_time_for (simplified: no holiday calendar lookup).
    /// </summary>
    public DateTime ComputeDeadline(DateTime from, decimal durationHours)
    {
        if (_serviceDays.Count == 0 || durationHours <= 0)
            return from.AddHours((double)durationHours);

        var remaining = TimeSpan.FromHours((double)durationHours);
        var cursor = from;

        for (var guard = 0; guard < 366 && remaining > TimeSpan.Zero; guard++)
        {
            var window = _serviceDays.FirstOrDefault(d => d.DayOfWeek == cursor.DayOfWeek);
            if (window == null)
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            var dayStart = cursor.Date.Add(window.StartTime);
            var dayEnd = cursor.Date.Add(window.EndTime);
            var windowStart = cursor > dayStart ? cursor : dayStart;

            if (windowStart >= dayEnd)
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            var available = dayEnd - windowStart;
            if (available >= remaining)
            {
                cursor = windowStart + remaining;
                remaining = TimeSpan.Zero;
            }
            else
            {
                remaining -= available;
                cursor = cursor.Date.AddDays(1);
            }
        }

        return cursor;
    }
}

/// <summary>Per-priority response/resolution targets within a Service Level Agreement.</summary>
public class ServiceLevelPriority : Entity<Guid>
{
    public Guid ServiceLevelAgreementId { get; set; }
    public string PriorityName { get; set; } = null!;
    public decimal ResponseTimeHours { get; set; }
    public decimal ResolutionTimeHours { get; set; }
    public bool IsDefault { get; set; }

    protected ServiceLevelPriority() { }

    public ServiceLevelPriority(Guid id, Guid serviceLevelAgreementId, string priorityName,
        decimal responseTimeHours, decimal resolutionTimeHours)
        : base(id)
    {
        ServiceLevelAgreementId = serviceLevelAgreementId;
        PriorityName = Check.NotNullOrWhiteSpace(priorityName, nameof(priorityName), ServiceLevelPriorityConsts.MaxPriorityNameLength);
        ResponseTimeHours = responseTimeHours;
        ResolutionTimeHours = resolutionTimeHours;
    }

    /// <summary>
    /// Validates priority targets (gotcha #827).
    /// </summary>
    public void Validate(bool applyOnResolution = true)
    {
        if (ResponseTimeHours <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Response Time for priority '{PriorityName}' must be greater than zero.");
        }

        if (applyOnResolution && ResolutionTimeHours > 0 && ResolutionTimeHours < ResponseTimeHours)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Resolution Time for priority '{PriorityName}' must be greater than or equal to Response Time.");
        }
    }
}

/// <summary>Working-hours window for one day of the week within a Service Level Agreement.</summary>
public class ServiceDay : Entity<Guid>
{
    public Guid ServiceLevelAgreementId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    protected ServiceDay() { }

    public ServiceDay(Guid id, Guid serviceLevelAgreementId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime)
        : base(id)
    {
        ServiceLevelAgreementId = serviceLevelAgreementId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }
}
