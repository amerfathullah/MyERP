using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Salary Slip — individual payslip for an employee for a payroll period.
/// Contains computed earnings and deductions from the salary structure.
/// Maps to ERPNext hr/doctype/salary_slip.
/// </summary>
public class SalarySlip : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? SalaryStructureId { get; set; }
    public Guid? PayrollEntryId { get; set; }

    public DateTime PostingDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int TotalWorkingDays { get; set; }
    public int PaymentDays { get; set; }
    public int LeavesWithoutPay { get; set; }

    public decimal GrossAmount { get; private set; }
    public decimal TotalDeductions { get; private set; }
    public decimal NetAmount { get; private set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<SalarySlipComponent> _earnings = new();
    public IReadOnlyList<SalarySlipComponent> Earnings => _earnings.AsReadOnly();

    private readonly List<SalarySlipComponent> _deductions = new();
    public IReadOnlyList<SalarySlipComponent> Deductions => _deductions.AsReadOnly();

    protected SalarySlip() { }

    public SalarySlip(Guid id, Guid companyId, Guid employeeId,
        DateTime startDate, DateTime endDate, DateTime postingDate, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        StartDate = startDate;
        EndDate = endDate;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddEarning(Guid componentId, string componentName, decimal amount, bool isStatutory = false)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _earnings.Add(new SalarySlipComponent(Guid.NewGuid(), Id, componentId, componentName, amount, true, isStatutory));
        RecalculateTotals();
    }

    public void AddDeduction(Guid componentId, string componentName, decimal amount, bool isStatutory = false)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _deductions.Add(new SalarySlipComponent(Guid.NewGuid(), Id, componentId, componentName, amount, false, isStatutory));
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        GrossAmount = _earnings.Sum(e => e.Amount);
        TotalDeductions = _deductions.Sum(d => d.Amount);
        NetAmount = GrossAmount - TotalDeductions;
    }
}

public class SalarySlipComponent : FullAuditedEntity<Guid>
{
    public Guid SalarySlipId { get; set; }
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsEarning { get; set; }
    public bool IsStatutory { get; set; }

    protected SalarySlipComponent() { }

    public SalarySlipComponent(Guid id, Guid slipId, Guid componentId,
        string componentName, decimal amount, bool isEarning, bool isStatutory) : base(id)
    {
        SalarySlipId = slipId;
        SalaryComponentId = componentId;
        ComponentName = componentName;
        Amount = amount;
        IsEarning = isEarning;
        IsStatutory = isStatutory;
    }
}
