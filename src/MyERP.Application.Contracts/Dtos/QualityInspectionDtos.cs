using System;
using MyERP.Core;
using MyERP.Inventory;
using Volo.Abp.Application.Dtos;

namespace MyERP.Dtos;

public class QualityInspectionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public InspectionType InspectionType { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? BatchNo { get; set; }
    public decimal SampleSize { get; set; }
    public DateTime InspectionDate { get; set; }
    public InspectionStatus Status { get; set; }
    public DocumentStatus DocStatus { get; set; }
    public string? Remarks { get; set; }
    public bool ManualInspection { get; set; }
    public QualityInspectionReadingDto[] Readings { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class QualityInspectionReadingDto : EntityDto<Guid>
{
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? ReadingValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public InspectionStatus Status { get; set; }
}

public class CreateQualityInspectionDto
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public InspectionType InspectionType { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? BatchNo { get; set; }
    public decimal SampleSize { get; set; }
    public DateTime InspectionDate { get; set; }
    public bool ManualInspection { get; set; }
    public CreateQualityInspectionReadingDto[] Readings { get; set; } = [];
}

public class CreateQualityInspectionReadingDto
{
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? ReadingValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }
}

public class GetQualityInspectionListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public InspectionStatus? Status { get; set; }
    public string? Filter { get; set; }
}
