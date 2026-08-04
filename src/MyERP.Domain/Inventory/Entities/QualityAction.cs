using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using MyERP.Core;

namespace MyERP.Inventory.Entities;

public enum QualityActionType
{
    Corrective,
    Preventive
}

public enum QualityActionStatus
{
    Open,
    Resolved,
    Closed
}

/// <summary>
/// Quality Action — tracks corrective or preventive actions from inspections.
/// </summary>
public class QualityAction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public QualityActionType ActionType { get; set; }
    public string ProblemDescription { get; set; } = null!;
    public string? Resolution { get; set; }
    
    public QualityActionStatus Status { get; private set; }

    public Guid? RelatedQualityGoalId { get; set; }
    public Guid? AssignedUserId { get; set; }

    protected QualityAction() { }

    public QualityAction(Guid id, Guid companyId, QualityActionType type, string problemDescription, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ActionType = type;
        ProblemDescription = problemDescription;
        Status = QualityActionStatus.Open;
        TenantId = tenantId;
    }

    public void Resolve(string resolution)
    {
        if (Status == QualityActionStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Resolution = resolution;
        Status = QualityActionStatus.Resolved;
    }

    public void Close()
    {
        Status = QualityActionStatus.Closed;
    }
}
