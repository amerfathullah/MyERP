using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using MyERP.Core;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Action — tracks corrective or preventive actions from inspections/reviews/feedbacks.
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
    public Guid? RelatedQualityReviewId { get; set; }
    public Guid? RelatedProcedureId { get; set; }
    public Guid? RelatedFeedbackId { get; set; }
    public Guid? AssignedUserId { get; set; }

    private readonly List<QualityActionResolution> _resolutions = new();
    public IReadOnlyList<QualityActionResolution> Resolutions => _resolutions.AsReadOnly();

    protected QualityAction() { }

    public QualityAction(Guid id, Guid companyId, QualityActionType type, string problemDescription, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ActionType = type;
        ProblemDescription = Check.NotNullOrWhiteSpace(problemDescription, nameof(problemDescription), maxLength: QualityManagementConsts.MaxProblemDescriptionLength);
        Status = QualityActionStatus.Open;
        TenantId = tenantId;
    }

    public void AddResolution(QualityActionResolution resolution)
    {
        _resolutions.Add(resolution);
        EvaluateStatus();
    }

    public void ClearResolutions()
    {
        _resolutions.Clear();
        EvaluateStatus();
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

    public void Reopen()
    {
        Status = QualityActionStatus.Open;
    }

    public void EvaluateStatus()
    {
        if (_resolutions.Count > 0 && _resolutions.All(r => r.Status == QualityActionStatus.Resolved || r.Status == QualityActionStatus.Closed))
        {
            Status = QualityActionStatus.Resolved;
        }
    }
}

public class QualityActionResolution : Entity<Guid>
{
    public Guid QualityActionId { get; set; }
    public string Problem { get; set; } = null!;
    public string ResolutionDetails { get; set; } = null!;
    public QualityActionStatus Status { get; set; } = QualityActionStatus.Open;

    protected QualityActionResolution() { }

    public QualityActionResolution(Guid id, Guid qualityActionId, string problem, string resolutionDetails)
        : base(id)
    {
        QualityActionId = qualityActionId;
        Problem = problem;
        ResolutionDetails = resolutionDetails;
        Status = QualityActionStatus.Open;
    }
}
