using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Telephony.Entities;

/// <summary>
/// Call Log — record of incoming and outgoing calls with telephony providers, duration, status, and linked CRM entities.
/// Maps to ERPNext telephony/doctype/call_log.
/// </summary>
public class CallLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
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

    protected CallLog() { }

    public CallLog(
        Guid id,
        string callId,
        string from,
        string to,
        CallDirection callDirection = CallDirection.Incoming,
        CallStatus status = CallStatus.Ringing,
        DateTime? startTime = null,
        string? medium = null,
        Guid? customerId = null,
        Guid? employeeUserId = null,
        Guid? callReceivedByEmployeeId = null,
        Guid? telephonyCallTypeId = null,
        Guid? tenantId = null)
        : base(id)
    {
        CallId = Check.NotNullOrWhiteSpace(callId, nameof(callId), TelephonyConsts.MaxCallIdLength);
        From = Check.NotNullOrWhiteSpace(from, nameof(from), TelephonyConsts.MaxPhoneNumberLength);
        To = Check.NotNullOrWhiteSpace(to, nameof(to), TelephonyConsts.MaxPhoneNumberLength);
        CallDirection = callDirection;
        Status = status;
        StartTime = startTime ?? DateTime.UtcNow;
        Medium = medium;
        CustomerId = customerId;
        EmployeeUserId = employeeUserId;
        CallReceivedByEmployeeId = callReceivedByEmployeeId;
        TelephonyCallTypeId = telephonyCallTypeId;
        TenantId = tenantId;
    }

    public void StartCall()
    {
        Status = CallStatus.InProgress;
        StartTime ??= DateTime.UtcNow;
    }

    public void CompleteCall(int durationSeconds, string? recordingUrl = null)
    {
        Status = CallStatus.Completed;
        Duration = Math.Max(0, durationSeconds);
        EndTime = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(recordingUrl))
        {
            RecordingUrl = recordingUrl;
        }
    }

    public void FailCall(CallStatus failureStatus)
    {
        if (failureStatus is CallStatus.Completed or CallStatus.InProgress or CallStatus.Ringing)
        {
            throw new BusinessException("MyERP:Telephony:001", "Invalid failure status for call.");
        }
        Status = failureStatus;
        EndTime = DateTime.UtcNow;
    }

    public void SetSummary(string? summary)
    {
        Summary = summary;
    }
}
