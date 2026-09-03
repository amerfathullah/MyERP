using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class CostCenterDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? CostCenterNumber { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCostCenterDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(200)] public string Name { get; set; } = null!;
    [StringLength(50)] public string? CostCenterNumber { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
}

public class GetCostCenterListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}

public class CostCenterTreeNodeDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CostCenterNumber { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
    public System.Collections.Generic.List<CostCenterTreeNodeDto> Children { get; set; } = new();
}
