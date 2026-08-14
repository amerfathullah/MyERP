using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using MyERP.Core;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Non-Conformance — tracks defect, incident, or deviation against a quality procedure.
/// </summary>
public class NonConformance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Subject { get; set; } = null!;
    public Guid? ProcedureId { get; set; }
    public string? ProcessOwner { get; set; }
    public string? Details { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
    public NonConformanceStatus Status { get; private set; } = NonConformanceStatus.Open;
    public DateTime? ResolutionDate { get; private set; }

    protected NonConformance() { }

    public NonConformance(Guid id, Guid companyId, string subject, Guid? procedureId = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Subject = Check.NotNullOrWhiteSpace(subject, nameof(subject), maxLength: QualityManagementConsts.MaxSubjectLength);
        ProcedureId = procedureId;
        Status = NonConformanceStatus.Open;
        TenantId = tenantId;
    }

    public void Resolve(string? correctiveAction = null, string? preventiveAction = null)
    {
        if (Status == NonConformanceStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        CorrectiveAction = correctiveAction ?? CorrectiveAction;
        PreventiveAction = preventiveAction ?? PreventiveAction;
        Status = NonConformanceStatus.Resolved;
        ResolutionDate = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = NonConformanceStatus.Cancelled;
    }

    public void Reopen()
    {
        Status = NonConformanceStatus.Open;
        ResolutionDate = null;
    }
}
