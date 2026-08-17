using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Appointment — a scheduled meeting, optionally booked by a prospect through the public portal.
/// Portal-created appointments start Unverified and only move to Open once the requester
/// confirms via the emailed verification link. Maps to ERPNext crm/doctype/appointment.
/// </summary>
public class Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string CustomerName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Details { get; set; }

    public DateTime ScheduledTime { get; set; }

    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Open;

    public bool CreatedThroughPortal { get; set; }
    public bool EmailVerified { get; private set; }

    /// <summary>Hash of the emailed verification token — never store the raw token.</summary>
    public string? VerificationTokenHash { get; set; }
    public DateTime? VerificationTokenExpiresOn { get; set; }

    /// <summary>"Lead" or "Customer" — resolved or auto-created when no party is supplied.</summary>
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }

    /// <summary>Sales agent assigned to this appointment (least-workload assignment).</summary>
    public Guid? AssignedAgentUserId { get; set; }

    protected Appointment() { }

    public Appointment(Guid id, Guid companyId, string customerName, DateTime scheduledTime,
        bool createdThroughPortal = false, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        CustomerName = Check.NotNullOrWhiteSpace(customerName, nameof(customerName), AppointmentConsts.MaxCustomerNameLength);
        ScheduledTime = scheduledTime;
        CreatedThroughPortal = createdThroughPortal;
        Status = createdThroughPortal ? AppointmentStatus.Unverified : AppointmentStatus.Open;
        TenantId = tenantId;
    }

    public void SetVerificationToken(string tokenHash, DateTime expiresOn)
    {
        if (Status != AppointmentStatus.Unverified)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        VerificationTokenHash = tokenHash;
        VerificationTokenExpiresOn = expiresOn;
    }

    /// <summary>Confirms the appointment via the emailed verification link. Only valid for portal-created, Unverified appointments.</summary>
    public void Verify(string tokenHash, DateTime asOfDate)
    {
        if (Status != AppointmentStatus.Unverified)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (VerificationTokenHash == null || VerificationTokenHash != tokenHash)
            throw new BusinessException(MyERPDomainErrorCodes.AppointmentNotVerified);

        if (VerificationTokenExpiresOn.HasValue && VerificationTokenExpiresOn.Value < asOfDate)
            throw new BusinessException(MyERPDomainErrorCodes.AppointmentNotVerified)
                .WithData("reason", "TokenExpired");

        EmailVerified = true;
        Status = AppointmentStatus.Open;
    }

    public void AssignAgent(Guid agentUserId)
    {
        AssignedAgentUserId = agentUserId;
    }

    public void LinkParty(string partyType, Guid partyId)
    {
        PartyType = partyType;
        PartyId = partyId;
    }

    public void Close()
    {
        if (Status != AppointmentStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AppointmentStatus.Closed;
    }

    /// <summary>Cancels an expired, never-verified portal appointment (scheduler cleanup).</summary>
    public void CancelExpiredUnverified(DateTime asOfDate)
    {
        if (Status != AppointmentStatus.Unverified)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!VerificationTokenExpiresOn.HasValue || VerificationTokenExpiresOn.Value >= asOfDate)
            throw new BusinessException(MyERPDomainErrorCodes.AppointmentNotVerified)
                .WithData("reason", "NotYetExpired");
        Status = AppointmentStatus.Closed;
    }
}
