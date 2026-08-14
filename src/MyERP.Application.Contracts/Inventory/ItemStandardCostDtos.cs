using System;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemStandardCostDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public decimal StandardRate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal? PreviousRate { get; set; }
    public int Status { get; set; }
    public Guid? RevaluationStockReconciliationId { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateItemStandardCostDto
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public decimal StandardRate { get; set; }
    public DateTime EffectiveDate { get; set; }
}

public class GetItemStandardCostListDto : CompanyFilteredPagedRequestDto
{
    public Guid? ItemId { get; set; }
}
