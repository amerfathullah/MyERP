using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class SalaryComponentDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Abbreviation { get; set; }
    public int ComponentType { get; set; }
    public bool IsStatutory { get; set; }
    public bool IsTaxApplicable { get; set; }
    public bool DependsOnPaymentDays { get; set; }
    public Guid? DefaultAccountId { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}

public class CreateUpdateSalaryComponentDto
{
    public string Name { get; set; } = null!;
    public string? Abbreviation { get; set; }
    public int ComponentType { get; set; }
    public bool IsStatutory { get; set; }
    public bool IsTaxApplicable { get; set; } = true;
    public bool DependsOnPaymentDays { get; set; } = true;
    public Guid? DefaultAccountId { get; set; }
    public string? Description { get; set; }
}
