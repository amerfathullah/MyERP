using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class SupplierGroupDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
}

public class CreateUpdateSupplierGroupDto
{
    [Required]
    [StringLength(TerritoryAndGroupsConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
}

public class GetSupplierGroupListDto : PagedAndSortedResultRequestDto
{
    public Guid? ParentId { get; set; }
    public bool? IsGroup { get; set; }
    public string? Filter { get; set; }
}
