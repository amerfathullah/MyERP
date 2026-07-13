using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Holiday List — calendar of non-working days for a company/branch.
/// Used by: leave calculation (exclude holidays), maintenance scheduling (backward-shift),
/// payroll (payment days calculation), workstation scheduling.
/// </summary>
public class HolidayList : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;
    public int Year { get; set; }

    /// <summary>Weekly off days (e.g., "Saturday,Sunday").</summary>
    public string? WeeklyOff { get; set; }

    public bool IsDefault { get; set; }

    private readonly List<Holiday> _holidays = new();
    public IReadOnlyList<Holiday> Holidays => _holidays.AsReadOnly();

    protected HolidayList() { }

    public HolidayList(Guid id, Guid companyId, string name, int year, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        Year = year;
        TenantId = tenantId;
    }

    public void AddHoliday(Holiday holiday)
    {
        _holidays.Add(holiday);
    }

    /// <summary>Check if a given date is a holiday.</summary>
    public bool IsHoliday(DateTime date)
    {
        return _holidays.Any(h => h.HolidayDate.Date == date.Date);
    }
}

/// <summary>Individual holiday date entry.</summary>
public class Holiday : Entity<Guid>
{
    public Guid HolidayListId { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;

    /// <summary>If true, this is a weekly off (auto-generated), not a named holiday.</summary>
    public bool IsWeeklyOff { get; set; }

    protected Holiday() { }

    public Holiday(Guid id, Guid holidayListId, DateTime holidayDate, string description, bool isWeeklyOff = false)
        : base(id)
    {
        HolidayListId = holidayListId;
        HolidayDate = holidayDate;
        Description = description;
        IsWeeklyOff = isWeeklyOff;
    }
}
