using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Assets;

public class AssetMaintenanceLogDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid AssetMaintenanceId { get; set; }
    public Guid AssetMaintenanceTaskId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string MaintenanceTask { get; set; } = null!;
    public MaintenancePeriodicity Periodicity { get; set; }
    public string? MaintenanceType { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public AssetMaintenanceStatus Status { get; set; }
    public Guid? AssignToEmployeeId { get; set; }
    public string? AssignTo { get; set; }
    public string? AssignToName { get; set; }
    public bool HasCertificate { get; set; }
    public string? CertificateDetails { get; set; }
    public string? CertificateNo { get; set; }
    public decimal? Cost { get; set; }
    public string? Description { get; set; }
    public string? ActionsPerformed { get; set; }
    public string? Remarks { get; set; }
}

public class CreateUpdateAssetMaintenanceLogDto
{
    public Guid CompanyId { get; set; }
    public Guid AssetMaintenanceId { get; set; }
    public Guid AssetMaintenanceTaskId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string MaintenanceTask { get; set; } = null!;
    public MaintenancePeriodicity Periodicity { get; set; }
    public string? MaintenanceType { get; set; }
    public DateTime DueDate { get; set; }
    public Guid? AssignToEmployeeId { get; set; }
    public string? AssignTo { get; set; }
    public string? AssignToName { get; set; }
    public bool HasCertificate { get; set; }
    public string? CertificateDetails { get; set; }
    public string? CertificateNo { get; set; }
    public decimal? Cost { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
}

public class CompleteAssetMaintenanceLogDto
{
    public DateTime CompletionDate { get; set; } = DateTime.UtcNow;
    public string? ActionsPerformed { get; set; }
    public string? CertificateNo { get; set; }
    public bool HasCertificate { get; set; }
    public string? CertificateDetails { get; set; }
    public decimal? Cost { get; set; }
    public string? Remarks { get; set; }
}

public class CreateAssetMaintenanceLogDto : CreateUpdateAssetMaintenanceLogDto
{
}

public class GetAssetMaintenanceLogListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? AssetMaintenanceId { get; set; }
    public AssetMaintenanceStatus? Status { get; set; }
    public string? Filter { get; set; }
}
