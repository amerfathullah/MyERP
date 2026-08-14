using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Assets;

public class AssetMaintenanceTaskDto : FullAuditedEntityDto<Guid>
{
    public Guid AssetMaintenanceId { get; set; }
    public string MaintenanceTask { get; set; } = null!;
    public MaintenancePeriodicity Periodicity { get; set; }
    public string? MaintenanceType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public DateTime? LastCompletionDate { get; set; }
    public Guid? AssignToEmployeeId { get; set; }
    public string? AssignTo { get; set; }
    public string? AssignToName { get; set; }
    public bool CertificateRequired { get; set; }
    public string? Description { get; set; }
    public string? CertificateNo { get; set; }
}

public class CreateUpdateAssetMaintenanceTaskDto
{
    public Guid? Id { get; set; }
    public string MaintenanceTask { get; set; } = null!;
    public MaintenancePeriodicity Periodicity { get; set; }
    public string? MaintenanceType { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public Guid? AssignToEmployeeId { get; set; }
    public string? AssignTo { get; set; }
    public string? AssignToName { get; set; }
    public bool CertificateRequired { get; set; }
    public string? Description { get; set; }
    public string? CertificateNo { get; set; }
}

public class AssetMaintenanceDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public Guid? MaintenanceManagerId { get; set; }
    public string? MaintenanceManagerName { get; set; }
    public Guid? MaintenanceTeamId { get; set; }
    public string? MaintenanceTeamName { get; set; }
    public List<AssetMaintenanceTaskDto> Tasks { get; set; } = new();
}

public class CreateUpdateAssetMaintenanceDto
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public Guid? MaintenanceManagerId { get; set; }
    public string? MaintenanceManagerName { get; set; }
    public Guid? MaintenanceTeamId { get; set; }
    public string? MaintenanceTeamName { get; set; }
    public List<CreateUpdateAssetMaintenanceTaskDto> Tasks { get; set; } = new();
}

public class CreateAssetMaintenanceDto : CreateUpdateAssetMaintenanceDto
{
}

public class GetAssetMaintenanceListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? AssetId { get; set; }
    public string? Filter { get; set; }
}
