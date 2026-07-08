using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Individual salary slip within a payroll run.
/// Contains per-employee statutory deductions and net pay.
/// </summary>
public class PayrollEntryLine : FullAuditedEntity<Guid>
{
    public Guid PayrollEntryId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;

    public decimal GrossSalary { get; set; }

    // Employee contributions (deducted from salary)
    public decimal EpfEmployee { get; set; }
    public decimal SocsoEmployee { get; set; }
    public decimal EisEmployee { get; set; }
    public decimal Pcb { get; set; }

    // Employer contributions (cost to company)
    public decimal EpfEmployer { get; set; }
    public decimal SocsoEmployer { get; set; }
    public decimal EisEmployer { get; set; }

    public decimal TotalDeductions => EpfEmployee + SocsoEmployee + EisEmployee + Pcb;
    public decimal NetSalary => GrossSalary - TotalDeductions;

    protected PayrollEntryLine() { }

    public PayrollEntryLine(Guid id, Guid payrollEntryId, Guid employeeId, string employeeName,
        decimal grossSalary, decimal epfEmployee, decimal epfEmployer,
        decimal socsoEmployee, decimal socsoEmployer, decimal eisEmployee, decimal eisEmployer, decimal pcb)
        : base(id)
    {
        PayrollEntryId = payrollEntryId;
        EmployeeId = employeeId;
        EmployeeName = employeeName;
        GrossSalary = grossSalary;
        EpfEmployee = epfEmployee;
        EpfEmployer = epfEmployer;
        SocsoEmployee = socsoEmployee;
        SocsoEmployer = socsoEmployer;
        EisEmployee = eisEmployee;
        EisEmployer = eisEmployer;
        Pcb = pcb;
    }
}
