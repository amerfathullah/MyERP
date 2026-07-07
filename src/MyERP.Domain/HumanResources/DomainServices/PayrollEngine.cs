using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Payroll calculation engine — computes EPF, SOCSO, EIS, PCB contributions.
/// All rates are data-driven via ContributionRule entities.
/// </summary>
public class PayrollEngine : DomainService
{
    private readonly IRepository<ContributionRule, Guid> _ruleRepository;

    public PayrollEngine(IRepository<ContributionRule, Guid> ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<PayrollCalculationResult> CalculateAsync(PayrollContext context)
    {
        var rules = await _ruleRepository.GetListAsync(r => r.IsActive);

        var result = new PayrollCalculationResult { GrossSalary = context.GrossSalary };

        // EPF
        var epfRule = FindApplicableRule(rules, ContributionType.EPF, context);
        if (epfRule != null)
        {
            var epfBase = epfRule.SalaryCeiling.HasValue
                ? Math.Min(context.GrossSalary, epfRule.SalaryCeiling.Value)
                : context.GrossSalary;
            result.EpfEmployee = Math.Round(epfBase * epfRule.EmployeeRate / 100m, 2);
            result.EpfEmployer = Math.Round(epfBase * epfRule.EmployerRate / 100m, 2);
        }

        // SOCSO
        var socsoRule = FindApplicableRule(rules, ContributionType.SOCSO, context);
        if (socsoRule != null)
        {
            var socsoBase = socsoRule.SalaryCeiling.HasValue
                ? Math.Min(context.GrossSalary, socsoRule.SalaryCeiling.Value)
                : context.GrossSalary;
            result.SocsoEmployee = Math.Round(socsoBase * socsoRule.EmployeeRate / 100m, 2);
            result.SocsoEmployer = Math.Round(socsoBase * socsoRule.EmployerRate / 100m, 2);
        }

        // EIS
        var eisRule = FindApplicableRule(rules, ContributionType.EIS, context);
        if (eisRule != null)
        {
            var eisBase = eisRule.SalaryCeiling.HasValue
                ? Math.Min(context.GrossSalary, eisRule.SalaryCeiling.Value)
                : context.GrossSalary;
            result.EisEmployee = Math.Round(eisBase * eisRule.EmployeeRate / 100m, 2);
            result.EisEmployer = Math.Round(eisBase * eisRule.EmployerRate / 100m, 2);
        }

        // PCB (simplified — real PCB uses LHDN's graduated schedule)
        var pcbRule = FindApplicableRule(rules, ContributionType.PCB, context);
        if (pcbRule != null)
        {
            result.Pcb = Math.Round(context.GrossSalary * pcbRule.EmployeeRate / 100m, 2);
        }

        result.TotalDeductions = result.EpfEmployee + result.SocsoEmployee + result.EisEmployee + result.Pcb;
        result.NetSalary = context.GrossSalary - result.TotalDeductions;

        return result;
    }

    private ContributionRule? FindApplicableRule(List<ContributionRule> rules, ContributionType type, PayrollContext context)
    {
        return rules
            .Where(r => r.Type == type && r.IsApplicable(context.PayrollDate, context.GrossSalary, context.EmployeeAge, context.Citizenship))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();
    }
}

public class PayrollContext
{
    public Guid EmployeeId { get; set; }
    public decimal GrossSalary { get; set; }
    public DateTime PayrollDate { get; set; }
    public int EmployeeAge { get; set; }
    public CitizenshipType Citizenship { get; set; }
}

public class PayrollCalculationResult
{
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
