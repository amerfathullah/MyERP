using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Creates accounting entries (Journal Entry) from a submitted Payroll Entry.
/// 
/// Standard payroll GL pattern:
///   DR  Salary Expense          (TotalGrossSalary + employer contributions)
///   CR  Salary Payable          (TotalNetSalary — net amount owed to employees)
///   CR  EPF Payable             (EpfEmployee + EpfEmployer)
///   CR  SOCSO Payable           (SocsoEmployee + SocsoEmployer)
///   CR  EIS Payable             (EisEmployee + EisEmployer)
///   CR  PCB/Tax Payable         (Pcb — withheld income tax)
///
/// The JE is auto-posted (submitted) on creation. Payroll entry keeps a reference
/// to the generated JE for audit trail.
///
/// Per ERPNext: payroll_entry.py → make_accrual_jv_entry()
/// - One JE per payroll run (not per employee)
/// - Aggregated amounts by category
/// - Cost center from Company default
/// </summary>
public class PayrollPostingService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Accounting.Entities.FiscalYear, Guid> _fiscalYearRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;

    public PayrollPostingService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Accounting.Entities.FiscalYear, Guid> fiscalYearRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator)
    {
        _journalEntryRepository = journalEntryRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Creates and posts a Journal Entry for the payroll run.
    /// Returns the created JE ID for linking back to the PayrollEntry.
    /// </summary>
    public async Task<Guid> PostPayrollAsync(PayrollEntry payroll)
    {
        var company = await _companyRepository.GetAsync(payroll.CompanyId);
        var jeNumber = await _numberGenerator.GenerateAsync("JournalEntry", payroll.CompanyId);

        // Resolve fiscal year for the posting date
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == payroll.CompanyId &&
            f.StartDate <= payroll.PostingDate &&
            f.EndDate >= payroll.PostingDate);
        var fiscalYearId = fy?.Id ?? payroll.CompanyId; // fallback

        var je = new JournalEntry(
            _guidGenerator.Create(),
            payroll.CompanyId,
            fiscalYearId,
            payroll.PostingDate,
            payroll.TenantId);

        je.EntryNumber = jeNumber;
        je.ReferenceType = "PayrollEntry";
        je.ReferenceId = payroll.Id;
        je.Narration = $"Payroll accrual for {payroll.PeriodLabel} ({payroll.PayrollNumber})";

        // Aggregate amounts
        decimal totalEpf = payroll.Lines.Sum(l => l.EpfEmployee + l.EpfEmployer);
        decimal totalSocso = payroll.Lines.Sum(l => l.SocsoEmployee + l.SocsoEmployer);
        decimal totalEis = payroll.Lines.Sum(l => l.EisEmployee + l.EisEmployer);
        decimal totalPcb = payroll.Lines.Sum(l => l.Pcb);
        decimal totalExpense = payroll.TotalGrossSalary + payroll.TotalEmployerContributions;

        // DR: Salary Expense (total cost to company)
        if (totalExpense > 0)
        {
            je.AddLine(
                accountId: company.DefaultExpenseAccountId ?? company.Id,
                amount: totalExpense,
                isDebit: true,
                description: $"Salary expense - {payroll.PeriodLabel}");
        }

        // CR: Salary Payable (net amount owed to employees)
        if (payroll.TotalNetSalary > 0)
        {
            je.AddLine(
                accountId: company.DefaultPayableAccountId ?? company.Id,
                amount: payroll.TotalNetSalary,
                isDebit: false,
                description: $"Net salary payable - {payroll.PeriodLabel}");
        }

        // CR: EPF Payable (employee + employer portions due to KWSP)
        if (totalEpf > 0)
        {
            je.AddLine(
                accountId: company.DefaultPayableAccountId ?? company.Id,
                amount: totalEpf,
                isDebit: false,
                description: $"EPF payable (employee + employer) - {payroll.PeriodLabel}");
        }

        // CR: SOCSO Payable
        if (totalSocso > 0)
        {
            je.AddLine(
                accountId: company.DefaultPayableAccountId ?? company.Id,
                amount: totalSocso,
                isDebit: false,
                description: $"SOCSO payable (employee + employer) - {payroll.PeriodLabel}");
        }

        // CR: EIS Payable
        if (totalEis > 0)
        {
            je.AddLine(
                accountId: company.DefaultPayableAccountId ?? company.Id,
                amount: totalEis,
                isDebit: false,
                description: $"EIS payable (employee + employer) - {payroll.PeriodLabel}");
        }

        // CR: PCB/Tax Payable (income tax withheld for LHDN)
        if (totalPcb > 0)
        {
            je.AddLine(
                accountId: company.DefaultPayableAccountId ?? company.Id,
                amount: totalPcb,
                isDebit: false,
                description: $"PCB payable (income tax) - {payroll.PeriodLabel}");
        }

        // Auto-post the JE
        je.Post();

        await _journalEntryRepository.InsertAsync(je, autoSave: true);
        return je.Id;
    }
}
