using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

/// <summary>
/// Books appointments, enforcing slot capacity against AppointmentBookingSettings and
/// auto-assigning the least-busy configured agent. Portal-created appointments start
/// Unverified and require VerifyAsync with the emailed token before use.
/// </summary>
[Authorize(MyERPPermissions.Leads.Default)]
public class AppointmentAppService : ApplicationService, IAppointmentAppService
{
    private readonly IRepository<Appointment, Guid> _repository;
    private readonly IRepository<AppointmentBookingSettings, Guid> _settingsRepository;
    private readonly IRepository<Lead, Guid> _leadRepository;

    public AppointmentAppService(
        IRepository<Appointment, Guid> repository,
        IRepository<AppointmentBookingSettings, Guid> settingsRepository,
        IRepository<Lead, Guid> leadRepository)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
        _leadRepository = leadRepository;
    }

    public async Task<AppointmentDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<AppointmentDto>> GetListAsync(GetAppointmentListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == input.CompanyId.Value);
        if (input.Status.HasValue)
            query = query.Where(a => a.Status == input.Status.Value);
        if (input.FromDate.HasValue)
            query = query.Where(a => a.ScheduledTime >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(a => a.ScheduledTime <= input.ToDate.Value);

        var totalCount = query.Count();
        var items = query.OrderBy(a => a.ScheduledTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<AppointmentDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<AppointmentDto> CreateAsync(CreateAppointmentDto input)
    {
        var settingsQuery = (await _settingsRepository.WithDetailsAsync()).AsQueryable();
        var settings = settingsQuery.FirstOrDefault(s => s.CompanyId == input.CompanyId);

        if (settings != null && settings.EnableScheduling)
        {
            if (!settings.IsWithinServiceWindow(input.ScheduledTime.DayOfWeek, input.ScheduledTime.TimeOfDay))
                throw new BusinessException(MyERPDomainErrorCodes.AppointmentOutsideServiceWindow)
                    .WithData("scheduledTime", input.ScheduledTime);

            var slotStart = input.ScheduledTime;
            var slotEnd = input.ScheduledTime.AddMinutes(settings.AppointmentDurationMinutes);
            var capacity = Math.Max(1, settings.NumberOfAgents);

            var overlapQuery = await _repository.GetQueryableAsync();
            var overlapCount = overlapQuery.Count(a => a.CompanyId == input.CompanyId
                && a.Status != AppointmentStatus.Closed
                && a.ScheduledTime < slotEnd
                && a.ScheduledTime.AddMinutes(settings.AppointmentDurationMinutes) > slotStart);

            if (overlapCount >= capacity)
                throw new BusinessException(MyERPDomainErrorCodes.AppointmentSlotFull)
                    .WithData("scheduledTime", input.ScheduledTime);
        }

        var entity = new Appointment(GuidGenerator.Create(), input.CompanyId, input.CustomerName,
            input.ScheduledTime, input.CreatedThroughPortal, CurrentTenant.Id)
        {
            Phone = input.Phone,
            Email = input.Email,
            Details = input.Details,
        };

        if (input.PartyId.HasValue && !string.IsNullOrWhiteSpace(input.PartyType))
        {
            entity.LinkParty(input.PartyType!, input.PartyId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(input.Email))
        {
            // Per ERPNext: auto-create a Lead when the appointment has no linked party.
            var leadNumber = $"LEAD-APT-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var lead = new Lead(GuidGenerator.Create(), input.CompanyId, leadNumber, input.CustomerName, CurrentTenant.Id)
            {
                Email = input.Email,
                Phone = input.Phone,
            };
            await _leadRepository.InsertAsync(lead);
            entity.LinkParty("Lead", lead.Id);
        }

        if (settings != null && settings.NumberOfAgents > 0)
        {
            var agentId = await ResolveLeastBusyAgentAsync(input.CompanyId, settings);
            entity.AssignAgent(agentId);
        }

        string? rawToken = null;
        if (input.CreatedThroughPortal)
        {
            var (token, tokenHash, expiresOn) = GenerateVerificationToken(settings);
            entity.SetVerificationToken(tokenHash, expiresOn);
            rawToken = token;
        }

        await _repository.InsertAsync(entity);
        var dto = MapToDto(entity);
        dto.VerificationToken = rawToken;
        return dto;
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<AppointmentDto> VerifyAsync(Guid id, VerifyAppointmentDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Verify(Hash(input.Token), DateTime.UtcNow);
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<AppointmentDto> CloseAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Close();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    /// <summary>Assigns the configured agent with the fewest open (non-Closed) appointments — ties broken by list order.</summary>
    private async Task<Guid> ResolveLeastBusyAgentAsync(Guid companyId, AppointmentBookingSettings settings)
    {
        var query = await _repository.GetQueryableAsync();
        var workload = query
            .Where(a => a.CompanyId == companyId && a.Status != AppointmentStatus.Closed && a.AssignedAgentUserId.HasValue)
            .GroupBy(a => a.AssignedAgentUserId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return settings.AgentUserIds
            .OrderBy(agentId => workload.GetValueOrDefault(agentId, 0))
            .First();
    }

    private (string Token, string TokenHash, DateTime ExpiresOn) GenerateVerificationToken(AppointmentBookingSettings? settings)
    {
        var token = Guid.NewGuid().ToString("N");
        var expiryMinutes = settings?.VerificationLinkExpiryMinutes ?? 15;
        return (token, Hash(token), DateTime.UtcNow.AddMinutes(expiryMinutes));
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static AppointmentDto MapToDto(Appointment e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        CustomerName = e.CustomerName,
        Phone = e.Phone,
        Email = e.Email,
        Details = e.Details,
        ScheduledTime = e.ScheduledTime,
        Status = e.Status,
        CreatedThroughPortal = e.CreatedThroughPortal,
        EmailVerified = e.EmailVerified,
        PartyType = e.PartyType,
        PartyId = e.PartyId,
        AssignedAgentUserId = e.AssignedAgentUserId,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}
