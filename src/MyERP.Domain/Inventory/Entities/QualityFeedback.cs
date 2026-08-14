using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Feedback Template — reusable parameters for collecting feedback ratings.
/// </summary>
public class QualityFeedbackTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string TemplateName { get; set; } = null!;

    private readonly List<QualityFeedbackTemplateParameter> _parameters = new();
    public IReadOnlyList<QualityFeedbackTemplateParameter> Parameters => _parameters.AsReadOnly();

    protected QualityFeedbackTemplate() { }

    public QualityFeedbackTemplate(Guid id, string templateName, Guid? tenantId = null)
        : base(id)
    {
        TemplateName = Check.NotNullOrWhiteSpace(templateName, nameof(templateName), maxLength: QualityManagementConsts.MaxNameLength);
        TenantId = tenantId;
    }

    public void AddParameter(QualityFeedbackTemplateParameter parameter)
    {
        _parameters.Add(parameter);
    }

    public void ClearParameters()
    {
        _parameters.Clear();
    }
}

public class QualityFeedbackTemplateParameter : Entity<Guid>
{
    public Guid QualityFeedbackTemplateId { get; set; }
    public string Parameter { get; set; } = null!;

    protected QualityFeedbackTemplateParameter() { }

    public QualityFeedbackTemplateParameter(Guid id, Guid templateId, string parameter)
        : base(id)
    {
        QualityFeedbackTemplateId = templateId;
        Parameter = Check.NotNullOrWhiteSpace(parameter, nameof(parameter), maxLength: QualityManagementConsts.MaxParameterNameLength);
    }
}

/// <summary>
/// Quality Feedback — user or customer feedback response with parameter ratings (1-5).
/// </summary>
public class QualityFeedback : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public QualityFeedbackDocumentType DocumentType { get; set; }
    public string DocumentName { get; set; } = null!;
    public Guid TemplateId { get; set; }
    public string? Remarks { get; set; }

    private readonly List<QualityFeedbackParameter> _parameters = new();
    public IReadOnlyList<QualityFeedbackParameter> Parameters => _parameters.AsReadOnly();

    protected QualityFeedback() { }

    public QualityFeedback(Guid id, Guid companyId, QualityFeedbackDocumentType documentType, string documentName, Guid templateId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        DocumentType = documentType;
        DocumentName = Check.NotNullOrWhiteSpace(documentName, nameof(documentName), maxLength: QualityManagementConsts.MaxDocumentNameLength);
        TemplateId = templateId;
        TenantId = tenantId;
    }

    public void AddParameter(QualityFeedbackParameter parameter)
    {
        _parameters.Add(parameter);
    }

    public void ClearParameters()
    {
        _parameters.Clear();
    }
}

public class QualityFeedbackParameter : Entity<Guid>
{
    public Guid QualityFeedbackId { get; set; }
    public string Parameter { get; set; } = null!;
    public int Rating { get; set; } // 1 to 5
    public string? Remarks { get; set; }

    protected QualityFeedbackParameter() { }

    public QualityFeedbackParameter(Guid id, Guid feedbackId, string parameter, int rating, string? remarks = null)
        : base(id)
    {
        QualityFeedbackId = feedbackId;
        Parameter = Check.NotNullOrWhiteSpace(parameter, nameof(parameter), maxLength: QualityManagementConsts.MaxParameterNameLength);
        Rating = Math.Clamp(rating, 1, 5);
        Remarks = remarks;
    }
}
