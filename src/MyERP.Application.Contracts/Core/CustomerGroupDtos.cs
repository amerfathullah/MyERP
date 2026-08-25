using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class CustomerGroupDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public decimal DefaultCreditLimit { get; set; }
}

public class CreateUpdateCustomerGroupDto
{
    [Required]
    [StringLength(TerritoryAndGroupsConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public decimal DefaultCreditLimit { get; set; }
}

public class GetCustomerGroupListDto : PagedAndSortedResultRequestDto
{
    public Guid? ParentId { get; set; }
    public bool? IsGroup { get; set; }
    public string? Filter { get; set; }
}
