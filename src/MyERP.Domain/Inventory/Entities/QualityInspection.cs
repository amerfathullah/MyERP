using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Inspection — records quality checks on incoming/outgoing stock.
/// Supports value-based, min/max, and formula-based inspection criteria.
/// Maps to ERPNext stock/doctype/quality_inspection.
/// </summary>
public class QualityInspection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }

    public InspectionType InspectionType { get; set; }

    /// <summary>Source document type: PurchaseReceipt, DeliveryNote, StockEntry, etc.</summary>
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    /// <summary>Specific child row in the source document.</summary>
    public Guid? ChildRowReference { get; set; }

    public string? BatchNo { get; set; }
    public decimal SampleSize { get; set; }
    public DateTime InspectionDate { get; set; }

    public InspectionStatus Status { get; private set; } = InspectionStatus.Draft;
    public DocumentStatus DocStatus { get; private set; } = DocumentStatus.Draft;
    public string? Remarks { get; set; }

    /// <summary>If true, manual override — inspector decides Accept/Reject regardless of readings.</summary>
    public bool ManualInspection { get; set; }

    private readonly List<QualityInspectionReading> _readings = new();
    public IReadOnlyList<QualityInspectionReading> Readings => _readings.AsReadOnly();

    protected QualityInspection() { }

    public QualityInspection(Guid id, Guid companyId, Guid itemId,
        InspectionType inspectionType, DateTime inspectionDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ItemId = itemId;
        InspectionType = inspectionType;
        InspectionDate = inspectionDate;
        TenantId = tenantId;
    }

    public void AddReading(string specification, string? expectedValue,
        decimal? minValue, decimal? maxValue, string? readingValue,
        bool isNumeric = false, bool formulaBased = false, string? formula = null)
    {
        if (DocStatus != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var reading = new QualityInspectionReading(
            Guid.NewGuid(), Id, specification, expectedValue,
            minValue, maxValue, readingValue, isNumeric, formulaBased, formula);
        _readings.Add(reading);
    }

    /// <summary>
    /// Evaluates all readings and determines overall inspection status.
    /// If ANY reading is Rejected and ManualInspection is false → Rejected.
    /// </summary>
    public void Evaluate()
    {
        foreach (var reading in _readings)
        {
            reading.Evaluate();
        }

        if (!ManualInspection && _readings.Any(r => r.Status == InspectionStatus.Rejected))
        {
            Status = InspectionStatus.Rejected;
        }
        else
        {
            Status = InspectionStatus.Accepted;
        }
    }

    public void Submit()
    {
        if (DocStatus != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_readings.Any())
            throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionHasNoReadings);

        Evaluate();
        DocStatus = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (DocStatus != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        DocStatus = DocumentStatus.Cancelled;
    }
}
