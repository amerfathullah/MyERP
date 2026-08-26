using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Telephony;

public class CallLogDto : FullAuditedEntityDto<Guid>
{
    public string CallId { get; set; } = null!;
    public string From { get; set; } = null!;
    public string To { get; set; } = null!;
    public CallDirection CallDirection { get; set; }
    public CallStatus Status { get; set; }
    public int Duration { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? RecordingUrl { get; set; }
    public string? Medium { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? EmployeeUserId { get; set; }
    public Guid? CallReceivedByEmployeeId { get; set; }
    public Guid? TelephonyCallTypeId { get; set; }
    public string? Summary { get; set; }
}

public class CreateCallLogDto
{
    [Required]
    [StringLength(TelephonyConsts.MaxCallIdLength)]
    public string CallId { get; set; } = null!;

    [Required]
    [StringLength(TelephonyConsts.MaxPhoneNumberLength)]
    public string From { get; set; } = null!;

    [Required]
    [StringLength(TelephonyConsts.MaxPhoneNumberLength)]
    public string To { get; set; } = null!;

    public CallDirection CallDirection { get; set; } = CallDirection.Incoming;
    public CallStatus Status { get; set; } = CallStatus.Ringing;
    public DateTime? StartTime { get; set; }

    [StringLength(TelephonyConsts.MaxMediumLength)]
    public string? Medium { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? EmployeeUserId { get; set; }
    public Guid? CallReceivedByEmployeeId { get; set; }
    public Guid? TelephonyCallTypeId { get; set; }
    public string? Summary { get; set; }
}

public class UpdateCallLogDto
{
    public CallStatus Status { get; set; }
    public int Duration { get; set; }
    public DateTime? EndTime { get; set; }

    [StringLength(TelephonyConsts.MaxUrlLength)]
    public string? RecordingUrl { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? EmployeeUserId { get; set; }
    public Guid? CallReceivedByEmployeeId { get; set; }
    public Guid? TelephonyCallTypeId { get; set; }

    [StringLength(TelephonyConsts.MaxSummaryLength)]
    public string? Summary { get; set; }
}

public class GetCallLogListDto : PagedAndSortedResultRequestDto
{
    public CallDirection? CallDirection { get; set; }
    public CallStatus? Status { get; set; }
    public Guid? TelephonyCallTypeId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Filter { get; set; }
}
