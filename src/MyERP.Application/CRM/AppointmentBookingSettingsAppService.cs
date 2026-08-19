using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class AppointmentBookingSettingsAppService : ApplicationService, IAppointmentBookingSettingsAppService
{
    private readonly IRepository<AppointmentBookingSettings, Guid> _repository;

    public AppointmentBookingSettingsAppService(IRepository<AppointmentBookingSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<AppointmentBookingSettingsDto?> GetForCompanyAsync(Guid companyId)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var settings = query.FirstOrDefault(s => s.CompanyId == companyId);
        return settings != null ? MapToDto(settings) : null;
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<AppointmentBookingSettingsDto> SaveAsync(SaveAppointmentBookingSettingsDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var existing = query.FirstOrDefault(s => s.CompanyId == input.CompanyId);

        if (existing == null)
        {
            existing = new AppointmentBookingSettings(GuidGenerator.Create(), input.CompanyId, CurrentTenant.Id);
            await _repository.InsertAsync(existing);
        }

        existing.AppointmentDurationMinutes = input.AppointmentDurationMinutes;
        existing.EnableScheduling = input.EnableScheduling;
        existing.EnableAppointmentPortal = input.EnableAppointmentPortal;
        existing.HolidayListId = input.HolidayListId;
        existing.AdvanceBookingDays = input.AdvanceBookingDays;
        existing.SetVerificationLinkExpiryMinutes(input.VerificationLinkExpiryMinutes);
        existing.ActionForExpiredUnverified = input.ActionForExpiredUnverified;
        existing.SetAgents(input.AgentUserIds);

        existing.ClearAvailability();
        foreach (var window in input.AvailabilityOfSlots)
        {
            if (window.ToTime < window.FromTime)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
            }

            existing.AddAvailability(new AppointmentAvailability(GuidGenerator.Create(), existing.Id,
                window.DayOfWeek, window.FromTime, window.ToTime));
        }

        await _repository.UpdateAsync(existing);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AppointmentBookingSettings", existing.Id,
            "Saved", existing.CompanyId,
            "AppointmentBookingSettings", "", "Saved", CurrentUser.Id,
            $"Appointment booking settings updated for company {existing.CompanyId}", CurrentTenant.Id));

        return MapToDto(existing);
    }

    private static AppointmentBookingSettingsDto MapToDto(AppointmentBookingSettings e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        AppointmentDurationMinutes = e.AppointmentDurationMinutes,
        EnableScheduling = e.EnableScheduling,
        EnableAppointmentPortal = e.EnableAppointmentPortal,
        HolidayListId = e.HolidayListId,
        AdvanceBookingDays = e.AdvanceBookingDays,
        VerificationLinkExpiryMinutes = e.VerificationLinkExpiryMinutes,
        ActionForExpiredUnverified = e.ActionForExpiredUnverified,
        AgentUserIds = e.AgentUserIds.ToList(),
        NumberOfAgents = e.NumberOfAgents,
        AvailabilityOfSlots = e.AvailabilityOfSlots.Select(a => new AppointmentAvailabilityDto
        {
            Id = a.Id,
            DayOfWeek = a.DayOfWeek,
            FromTime = a.FromTime,
            ToTime = a.ToTime,
        }).ToList(),
    };
}
