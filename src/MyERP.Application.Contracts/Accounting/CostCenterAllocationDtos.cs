using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class CostCenterAllocationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid MainCostCenterId { get; set; }
    public DateTime ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public List<CostCenterAllocationEntryDto> Entries { get; set; } = new();
}

public class CostCenterAllocationEntryDto
{
    public Guid Id { get; set; }
    public Guid ChildCostCenterId { get; set; }
    public decimal Percentage { get; set; }
}

public class CreateCostCenterAllocationDto
{
    public Guid CompanyId { get; set; }
    public Guid MainCostCenterId { get; set; }
    public DateTime ValidFrom { get; set; }
    public List<CreateCostCenterAllocationEntryDto> Entries { get; set; } = new();
}

public class CreateCostCenterAllocationEntryDto
{
    public Guid ChildCostCenterId { get; set; }
    public decimal Percentage { get; set; }
}
