using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Inspection Reading — individual test parameter within an inspection.
/// Supports value-based (exact match), min/max range, and formula-based evaluation.
/// </summary>
public class QualityInspectionReading : FullAuditedEntity<Guid>
{
    public Guid QualityInspectionId { get; set; }
    public string Specification { get; set; } = null!;

    /// <summary>For value-based: exact expected value.</summary>
    public string? ExpectedValue { get; set; }

    /// <summary>For numeric: min/max range.</summary>
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    /// <summary>Actual reading value (text or numeric as string).</summary>
    public string? ReadingValue { get; set; }

    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }

    public InspectionStatus Status { get; internal set; } = InspectionStatus.Draft;

    protected QualityInspectionReading() { }

    public QualityInspectionReading(Guid id, Guid qualityInspectionId,
        string specification, string? expectedValue,
        decimal? minValue, decimal? maxValue, string? readingValue,
        bool isNumeric, bool formulaBased, string? formula)
        : base(id)
    {
        QualityInspectionId = qualityInspectionId;
        Specification = specification;
        ExpectedValue = expectedValue;
        MinValue = minValue;
        MaxValue = maxValue;
        ReadingValue = readingValue;
        IsNumeric = isNumeric;
        FormulaBased = formulaBased;
        Formula = formula;
    }

    /// <summary>
    /// Evaluates this reading against criteria.
    /// Value-based: exact match. Numeric: min ≤ value ≤ max. Formula: truthy result.
    /// </summary>
    public void Evaluate()
    {
        if (FormulaBased)
        {
            if (string.IsNullOrWhiteSpace(Formula))
            {
                throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionInvalidFormula)
                    .WithData("specification", Specification)
                    .WithData("reason", "Formula is missing.");
            }

            // MyERP stores a single ReadingValue per parameter (unlike ERPNext's
            // reading_1..reading_10 samples), so both "reading_1" and "mean" resolve
            // to that same value — templates should compare against just reading_1/mean.
            if (!decimal.TryParse(ReadingValue, out var numericReading))
            {
                Status = InspectionStatus.Rejected;
                return;
            }

            var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["reading_1"] = numericReading,
                ["mean"] = numericReading,
            };

            bool accepted;
            try
            {
                accepted = AcceptanceFormulaEvaluator.EvaluateBoolean(Formula, variables);
            }
            catch (FormatException ex)
            {
                throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionInvalidFormula)
                    .WithData("specification", Specification)
                    .WithData("reason", ex.Message);
            }

            Status = accepted ? InspectionStatus.Accepted : InspectionStatus.Rejected;
            return;
        }

        if (IsNumeric)
        {
            if (decimal.TryParse(ReadingValue, out var numValue))
            {
                bool inRange = (!MinValue.HasValue || numValue >= MinValue.Value)
                            && (!MaxValue.HasValue || numValue <= MaxValue.Value);
                Status = inRange ? InspectionStatus.Accepted : InspectionStatus.Rejected;
            }
            else
            {
                Status = InspectionStatus.Rejected;
            }
            return;
        }

        // Value-based: exact match
        Status = string.Equals(ReadingValue?.Trim(), ExpectedValue?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? InspectionStatus.Accepted
            : InspectionStatus.Rejected;
    }
}
