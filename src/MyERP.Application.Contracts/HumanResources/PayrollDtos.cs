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

/// <summary>
/// DTO for creating a bank payment JE from a submitted payroll.
/// Per ERPNext payroll_entry.py make_bank_entry():
/// Creates JE: DR Salary Payable → CR Bank for total net salary.
/// </summary>
public class CreatePayrollBankEntryDto
{
    [Required]
    public Guid PayrollEntryId { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }

    /// <summary>Payment reference number (e.g., cheque number or bank transfer ref).</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Payment date (defaults to payroll posting date if not set).</summary>
    public DateTime? PaymentDate { get; set; }
}

public class PayrollBankEntryResultDto
{
    public Guid JournalEntryId { get; set; }
    public string JournalEntryNumber { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int EmployeeCount { get; set; }
}

/// <summary>
/// Preview of employees that will be included in a payroll run.
/// Per ERPNext: "Get Employees" step shows eligible employees before processing.
/// </summary>
public class PayrollPreviewDto
{
    public int EmployeeCount { get; set; }
    public decimal EstimatedGrossTotal { get; set; }
    public List<PayrollEmployeePreviewDto> Employees { get; set; } = new();
}

public class PayrollEmployeePreviewDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public decimal BasicSalary { get; set; }
}
