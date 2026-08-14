using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class SalaryStructureDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsHourlyBased { get; set; }
    public string PayrollFrequency { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public SalaryStructureDetailDto[] Details { get; set; } = [];
}

public class SalaryStructureDetailDto
{
    public Guid Id { get; set; }
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Formula { get; set; }
}

public class CreateSalaryStructureDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsHourlyBased { get; set; }
    public string PayrollFrequency { get; set; } = "Monthly";
    public string? Description { get; set; }
    public CreateSalaryStructureDetailDto[] Details { get; set; } = [];
}

public class CreateSalaryStructureDetailDto
{
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Formula { get; set; }
}
