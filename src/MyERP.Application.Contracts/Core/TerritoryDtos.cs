using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class TerritoryDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsGroup { get; set; }
    public Guid? TerritoryManagerId { get; set; }
    public string? TerritoryManagerName { get; set; }
    public List<TerritoryTargetDto> Targets { get; set; } = new();
}

public class TerritoryTargetDto : EntityDto<Guid>
{
    public Guid TerritoryId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public string? FiscalYearName { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? ItemGroupName { get; set; }
    public Guid? DistributionId { get; set; }
}

public class CreateTerritoryTargetDto
{
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? DistributionId { get; set; }
}

public class CreateUpdateTerritoryDto
{
    [Required]
    [StringLength(TerritoryAndGroupsConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? TerritoryManagerId { get; set; }
}

public class GetTerritoryListDto : PagedAndSortedResultRequestDto
{
    public Guid? ParentId { get; set; }
    public bool? IsGroup { get; set; }
    public string? Filter { get; set; }
}
