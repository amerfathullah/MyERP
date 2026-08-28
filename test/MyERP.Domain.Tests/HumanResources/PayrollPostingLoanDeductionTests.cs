using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Regression coverage: PostPayrollAsync built the accrual Journal Entry crediting
/// TotalNetSalary (already reduced by LoanDeduction, since NetSalary = Gross - TotalDeductions,
/// which includes LoanDeduction) but never credited an offsetting line for the loan portion —
/// so total debits exceeded total credits by exactly the loan deduction, and Post()/Validate()
/// threw UnbalancedJournalEntry for any payroll run touching an employee with an active loan
/// (round-95 fix).
/// </summary>
public class PayrollPostingLoanDeductionTests
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepository = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<Loan, Guid> _loanRepository = Substitute.For<IRepository<Loan, Guid>>();
    private readonly IDocumentNumberGenerator _numberGenerator = Substitute.For<IDocumentNumberGenerator>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    private readonly PayrollPostingService _service;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _payableAccountId = Guid.NewGuid();
    private readonly Guid _expenseAccountId = Guid.NewGuid();
    private readonly Guid _loanAccountId = Guid.NewGuid();

    public PayrollPostingLoanDeductionTests()
    {
        _service = new PayrollPostingService(
            _journalEntryRepository, _companyRepository, _fiscalYearRepository,
            _loanRepository, _numberGenerator, _guidGenerator);

        _numberGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DateTime?>())
            .Returns("JE-2026-0001");
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

    [Fact]
    public async Task PostPayrollAsync_WithLoanDeduction_ProducesBalancedJournalEntry()
    {
        var company = new Company(_companyId, "Loan Deduction Test Co")
        {
            DefaultPayableAccountId = _payableAccountId,
            DefaultExpenseAccountId = _expenseAccountId,
        };
        _companyRepository.GetAsync(_companyId).Returns(company);

        var loanId = Guid.NewGuid();
        var loan = new Loan(loanId, _companyId, Guid.NewGuid(), "LOAN-001",
            LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 10000m, 5m, 12)
        {
            LoanAccountId = _loanAccountId,
        };
        _loanRepository.FindAsync(loanId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>()).Returns(loan);

        var payroll = new PayrollEntry(Guid.NewGuid(), _companyId, "PAY-001", 2026, 7, DateTime.UtcNow);
        payroll.AddLine(Guid.NewGuid(), "Employee", 5000m, 550m, 650m, 9.95m, 34.85m, 9.95m, 9.95m, 200m);
        payroll.Lines[0].LoanDeduction = 500m;
        payroll.Lines[0].LoanId = loanId;
        payroll.RecalculateTotals();

        JournalEntry? capturedJe = null;
        await _journalEntryRepository.InsertAsync(Arg.Do<JournalEntry>(je => capturedJe = je), autoSave: true);

        // Should not throw UnbalancedJournalEntry — the previously-missing loan credit line
        // is what keeps DR Salary Expense equal to the sum of every CR line below it.
        var journalEntryId = await _service.PostPayrollAsync(payroll);

        journalEntryId.ShouldNotBe(Guid.Empty);
        await _journalEntryRepository.Received(1).InsertAsync(Arg.Any<JournalEntry>(), autoSave: true);
        capturedJe.ShouldNotBeNull();
        capturedJe!.TotalDebit.ShouldBe(capturedJe.TotalCredit);
        capturedJe.Lines.ShouldContain(l => l.AccountId == _loanAccountId && !l.IsDebit && l.Amount == 500m);
    }
}
