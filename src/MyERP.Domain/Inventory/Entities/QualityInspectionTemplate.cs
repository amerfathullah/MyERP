using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Inspection Template — reusable template that auto-populates QI readings.
/// Per ERPNext: templates define inspection criteria per item/BOM/operation.
/// When a QI is created, the template's parameters are copied as readings.
/// </summary>
public class QualityInspectionTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Linked item for item-specific templates. Null = generic template.</summary>
    public Guid? ItemId { get; set; }

    /// <summary>Linked BOM for BOM-specific templates.</summary>
    public Guid? BomId { get; set; }

    public bool IsEnabled { get; set; } = true;

    private readonly List<QualityInspectionParameter> _parameters = new();
    public IReadOnlyList<QualityInspectionParameter> Parameters => _parameters.AsReadOnly();

    protected QualityInspectionTemplate() { }

    public QualityInspectionTemplate(Guid id, string name, Guid? tenantId = null)
        : base(id)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Name = name;
        TenantId = tenantId;
    }

    public void AddParameter(Guid parameterId, string specification,
        string? expectedValue = null, decimal? minValue = null, decimal? maxValue = null,
        bool isNumeric = false, bool formulaBased = false, string? formula = null,
        string? acceptanceCriteria = null)
    {
        _parameters.Add(new QualityInspectionParameter(parameterId, Id, specification,
            expectedValue, minValue, maxValue, isNumeric, formulaBased, formula, acceptanceCriteria));
    }

    public void ClearParameters()
    {
        _parameters.Clear();
    }

    /// <summary>
    /// Validates template parameters per ERPNext PR #58940 / commits aae67879ae, 0ae42aec9b, a6203ca59df034f21984175a25cbc1233515f41c:
    /// 1. Reject duplicate parameters and identify row number and specification.
    /// 2. For numeric parameters, minimum value cannot exceed maximum value.
    /// 3. For non-numeric non-formula parameters, acceptance criteria value is required.
    /// 4. For formula-based criteria, acceptance formula is required and cannot be whitespace.
    /// </summary>
    public void Validate()
    {
        var seenSpecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _parameters.Count; i++)
        {
            var p = _parameters[i];
            var rowIdx = i + 1;

            if (seenSpecs.Contains(p.Specification))
            {
                var msg = $"Row #{rowIdx}: Parameter {p.Specification} is added more than once";
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", msg)
                    .WithData("message", msg);
            }
            seenSpecs.Add(p.Specification);

            if (p.FormulaBased)
            {
                if (string.IsNullOrWhiteSpace(p.Formula))
                {
                    var msg = $"Row #{rowIdx}: Acceptance Criteria Formula is required for parameter {p.Specification}";
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", msg)
                        .WithData("message", msg);
                }
            }
            else if (p.IsNumeric)
            {
                if (p.MinValue.HasValue && p.MaxValue.HasValue && p.MinValue.Value > p.MaxValue.Value)
                {
                    var msg = $"Row #{rowIdx}: Minimum Value cannot be greater than Maximum Value for parameter {p.Specification}";
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", msg)
                        .WithData("message", msg);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(p.ExpectedValue))
                {
                    var msg = $"Row #{rowIdx}: Acceptance Criteria Value is required for parameter {p.Specification}";
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", msg)
                        .WithData("message", msg);
                }
            }
        }
    }

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;
}

/// <summary>
/// Quality Inspection Parameter — defines a single inspection criterion within a template.
/// Copied to QualityInspectionReading when QI is created from this template.
/// </summary>
public class QualityInspectionParameter : FullAuditedEntity<Guid>
{
    public Guid QualityInspectionTemplateId { get; set; }
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }

    /// <summary>Human-readable acceptance criteria description.</summary>
    public string? AcceptanceCriteria { get; set; }

    protected QualityInspectionParameter() { }

    public QualityInspectionParameter(Guid id, Guid templateId,
        string specification, string? expectedValue,
        decimal? minValue, decimal? maxValue, bool isNumeric,
        bool formulaBased, string? formula, string? acceptanceCriteria)
        : base(id)
    {
        QualityInspectionTemplateId = templateId;
        Specification = specification;
        ExpectedValue = expectedValue;
        MinValue = minValue;
        MaxValue = maxValue;
        IsNumeric = isNumeric;
        FormulaBased = formulaBased;
        Formula = formula;
        AcceptanceCriteria = acceptanceCriteria;
    }
}
