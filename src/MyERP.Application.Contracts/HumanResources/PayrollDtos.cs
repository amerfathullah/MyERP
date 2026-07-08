using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class PayrollEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string PayrollNumber { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public decimal TotalGrossSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public string Status { get; set; } = null!;
    public List<PayrollEntryLineDto> Lines { get; set; } = new();
}

public class PayrollEntryLineDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public decimal GrossSalary { get; set; }
    public decimal EpfEmployee { get; set; }
    public decimal EpfEmployer { get; set; }
    public decimal SocsoEmployee { get; set; }
    public decimal SocsoEmployer { get; set; }
    public decimal EisEmployee { get; set; }
    public decimal EisEmployer { get; set; }
    public decimal Pcb { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }
}

public class CreatePayrollEntryDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [Range(2020, 2100)]
    public int Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int Month { get; set; }
}
