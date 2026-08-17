using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public class AppointmentAvailabilityDto
{
    public Guid Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
}

public class AppointmentBookingSettingsDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int AppointmentDurationMinutes { get; set; }
    public bool EnableScheduling { get; set; }
    public bool EnableAppointmentPortal { get; set; }
    public Guid? HolidayListId { get; set; }
    public int AdvanceBookingDays { get; set; }
    public int VerificationLinkExpiryMinutes { get; set; }
    public ExpiredAppointmentAction ActionForExpiredUnverified { get; set; }
    public List<Guid> AgentUserIds { get; set; } = new();
    public int NumberOfAgents { get; set; }
    public List<AppointmentAvailabilityDto> AvailabilityOfSlots { get; set; } = new();
}

public class SaveAppointmentAvailabilityDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
}

public class SaveAppointmentBookingSettingsDto
{
    public Guid CompanyId { get; set; }
    public int AppointmentDurationMinutes { get; set; } = 60;
    public bool EnableScheduling { get; set; }
    public bool EnableAppointmentPortal { get; set; }
    public Guid? HolidayListId { get; set; }
    public int AdvanceBookingDays { get; set; } = 30;
    public int VerificationLinkExpiryMinutes { get; set; } = 15;
    public ExpiredAppointmentAction ActionForExpiredUnverified { get; set; } = ExpiredAppointmentAction.CancelAppointment;
    public List<Guid> AgentUserIds { get; set; } = new();
    public List<SaveAppointmentAvailabilityDto> AvailabilityOfSlots { get; set; } = new();
}

public interface IAppointmentBookingSettingsAppService : IApplicationService
{
    Task<AppointmentBookingSettingsDto?> GetForCompanyAsync(Guid companyId);
    Task<AppointmentBookingSettingsDto> SaveAsync(SaveAppointmentBookingSettingsDto input);
}
