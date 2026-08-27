using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class SalarySlipDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetAmount { get; set; }
    public int Status { get; set; }
    public List<SalarySlipComponentDto> Earnings { get; set; } = new();
    public List<SalarySlipComponentDto> Deductions { get; set; } = new();
}

public class SalarySlipComponentDto : EntityDto<Guid>
{
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsStatutory { get; set; }
}

/// <summary>Create/update a one-off or adjustment Salary Slip manually — for cases outside
/// a bulk Payroll Entry run (mid-cycle hire, correction, bonus). Per ERPNext: Salary Slip
/// can be created individually, not only via Payroll Entry.</summary>
public class CreateSalarySlipDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    [Required] public DateTime StartDate { get; set; }
    [Required] public DateTime EndDate { get; set; }
    public int TotalWorkingDays { get; set; }
    public int PaymentDays { get; set; }
    public int LeavesWithoutPay { get; set; }
    public List<SalarySlipComponentInputDto> Earnings { get; set; } = new();
    public List<SalarySlipComponentInputDto> Deductions { get; set; } = new();
}

public class SalarySlipComponentInputDto
{
    [Required] public Guid SalaryComponentId { get; set; }
    [Required] public string ComponentName { get; set; } = null!;
    [Required] public decimal Amount { get; set; }
    public bool IsStatutory { get; set; }
}
