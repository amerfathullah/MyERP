using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public class AppointmentDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Details { get; set; }
    public DateTime ScheduledTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public bool CreatedThroughPortal { get; set; }
    public bool EmailVerified { get; set; }
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public Guid? AssignedAgentUserId { get; set; }

    /// <summary>
    /// Populated only on the CreateAsync response for portal-created appointments — this is the
    /// raw token to email the requester (no email pipeline is wired up yet, so it's returned here
    /// instead). Never populated on Get/List responses.
    /// </summary>
    public string? VerificationToken { get; set; }
}

public class CreateAppointmentDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(AppointmentConsts.MaxCustomerNameLength)] public string CustomerName { get; set; } = null!;
    [StringLength(AppointmentConsts.MaxPhoneLength)] public string? Phone { get; set; }
    [StringLength(AppointmentConsts.MaxEmailLength)] public string? Email { get; set; }
    [StringLength(AppointmentConsts.MaxDetailsLength)] public string? Details { get; set; }
    [Required] public DateTime ScheduledTime { get; set; }
    public bool CreatedThroughPortal { get; set; }
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
}

public class VerifyAppointmentDto
{
    [Required] public string Token { get; set; } = null!;
}

public class GetAppointmentListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public AppointmentStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public interface IAppointmentAppService : IApplicationService
{
    Task<AppointmentDto> GetAsync(Guid id);
    Task<PagedResultDto<AppointmentDto>> GetListAsync(GetAppointmentListDto input);

    /// <summary>Books an appointment. Checks slot capacity against AppointmentBookingSettings and
    /// auto-assigns the least-busy agent. Portal-created appointments start Unverified.</summary>
    Task<AppointmentDto> CreateAsync(CreateAppointmentDto input);

    Task<AppointmentDto> VerifyAsync(Guid id, VerifyAppointmentDto input);
    Task<AppointmentDto> CloseAsync(Guid id);
    Task DeleteAsync(Guid id);
}
