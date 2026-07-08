using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Payroll Entry — represents a payroll run for a specific period.
/// Maps to ERPNext hr/doctype/payroll_entry.
/// Contains calculated salary slips for all employees processed.
/// </summary>
public class PayrollEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string PayrollNumber { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Period label, e.g., "July 2026".</summary>
    public string PeriodLabel => $"{new DateTime(Year, Month, 1):MMMM yyyy}";

    public DateTime PostingDate { get; set; }
    public string CurrencyCode { get; set; } = "MYR";

    public decimal TotalGrossSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<PayrollEntryLine> _lines = new();
    public IReadOnlyList<PayrollEntryLine> Lines => _lines.AsReadOnly();

    protected PayrollEntry() { }

    public PayrollEntry(Guid id, Guid companyId, string payrollNumber, int year, int month, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PayrollNumber = Check.NotNullOrWhiteSpace(payrollNumber, nameof(payrollNumber));
        Year = year;
        Month = month;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddLine(Guid employeeId, string employeeName, decimal grossSalary,
        decimal epfEmployee, decimal epfEmployer, decimal socsoEmployee, decimal socsoEmployer,
        decimal eisEmployee, decimal eisEmployer, decimal pcb)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var line = new PayrollEntryLine(Guid.NewGuid(), Id, employeeId, employeeName, grossSalary,
            epfEmployee, epfEmployer, socsoEmployee, socsoEmployer, eisEmployee, eisEmployer, pcb);
        _lines.Add(line);
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_lines.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        TotalGrossSalary = _lines.Sum(l => l.GrossSalary);
        TotalDeductions = _lines.Sum(l => l.TotalDeductions);
        TotalNetSalary = _lines.Sum(l => l.NetSalary);
        TotalEmployerContributions = _lines.Sum(l => l.EpfEmployer + l.SocsoEmployer + l.EisEmployer);
    }
}
