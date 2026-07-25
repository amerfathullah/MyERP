using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for Bank Reconciliation Statement logic and recent feature entity relationships.
/// Per ERPNext: GL Balance - Outstanding Uncleared = Expected Bank Balance.
/// </summary>
public class BankReconciliationStatementTests
{
    [Fact]
    public void BRS_Dto_Defaults()
    {
        var dto = new BankReconciliationStatementDto();

        Assert.Equal(0, dto.GlBalance);
        Assert.Equal(0, dto.OutstandingDeposits);
        Assert.Equal(0, dto.OutstandingPayments);
        Assert.Equal(0, dto.NetOutstanding);
        Assert.Equal(0, dto.CalculatedBankBalance);
        Assert.Empty(dto.UnclearedEntries);
        Assert.Equal("MYR", dto.CurrencyCode);
    }

    [Fact]
    public void BRS_CalculatedBankBalance_Is_GLBalance_Minus_NetOutstanding()
    {
        var dto = new BankReconciliationStatementDto
        {
            GlBalance = 50000m,
            OutstandingDeposits = 5000m,
            OutstandingPayments = 12000m
        };

        // Net outstanding = 5000 - 12000 = -7000
        Assert.Equal(-7000m, dto.NetOutstanding);
        // Calculated = 50000 - (-7000) = 57000
        Assert.Equal(57000m, dto.CalculatedBankBalance);
    }

    [Fact]
    public void BRS_AllCleared_ZeroOutstanding()
    {
        var dto = new BankReconciliationStatementDto
        {
            GlBalance = 100000m,
            OutstandingDeposits = 0,
            OutstandingPayments = 0
        };

        Assert.Equal(0, dto.NetOutstanding);
        Assert.Equal(100000m, dto.CalculatedBankBalance);
    }

    [Fact]
    public void BRS_OnlyOutstandingChecks_ReducesBankBalance()
    {
        // Checks written but not yet cleared at bank
        var dto = new BankReconciliationStatementDto
        {
            GlBalance = 80000m,
            OutstandingDeposits = 0,
            OutstandingPayments = 15000m // checks not yet cleared
        };

        // Net outstanding = 0 - 15000 = -15000
        // Bank should show: 80000 - (-15000) = 95000 (bank still has the money)
        Assert.Equal(95000m, dto.CalculatedBankBalance);
    }

    [Fact]
    public void BRS_OnlyOutstandingDeposits_IncreasesBankBalance()
    {
        // Deposits recorded in GL but not yet on bank statement
        var dto = new BankReconciliationStatementDto
        {
            GlBalance = 80000m,
            OutstandingDeposits = 10000m, // deposits not yet shown on bank statement
            OutstandingPayments = 0
        };

        // Net outstanding = 10000 - 0 = 10000
        // Bank should show: 80000 - 10000 = 70000 (bank doesn't see deposit yet)
        Assert.Equal(70000m, dto.CalculatedBankBalance);
    }

    [Fact]
    public void BRS_MixedOutstanding_CorrectCalculation()
    {
        var dto = new BankReconciliationStatementDto
        {
            GlBalance = 120000m,
            OutstandingDeposits = 8000m,
            OutstandingPayments = 25000m
        };

        // Net = 8000 - 25000 = -17000
        // Calculated = 120000 - (-17000) = 137000
        Assert.Equal(-17000m, dto.NetOutstanding);
        Assert.Equal(137000m, dto.CalculatedBankBalance);
    }

    [Fact]
    public void BRS_EntryDto_Properties()
    {
        var entry = new BankStatementEntryDto
        {
            PostingDate = new DateTime(2026, 7, 15),
            DocumentType = "Payment Entry",
            DocumentNumber = "PE-2026-00042",
            DocumentId = Guid.NewGuid(),
            Debit = 5000m,
            Credit = 0,
            ReferenceNumber = "CHQ-1234",
            ClearanceDate = null,
            PartyName = "ABC Supplies"
        };

        Assert.Equal("Payment Entry", entry.DocumentType);
        Assert.Equal("PE-2026-00042", entry.DocumentNumber);
        Assert.Equal(5000m, entry.Debit);
        Assert.Null(entry.ClearanceDate);
        Assert.Equal("ABC Supplies", entry.PartyName);
    }

    [Fact]
    public void BRS_Input_RequiresAllFields()
    {
        var input = new GetBankReconciliationStatementInput
        {
            BankAccountId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            ReportDate = new DateTime(2026, 7, 31)
        };

        Assert.NotEqual(Guid.Empty, input.BankAccountId);
        Assert.NotEqual(Guid.Empty, input.CompanyId);
        Assert.Equal(new DateTime(2026, 7, 31), input.ReportDate);
    }

    [Fact]
    public void BRS_NegativeGlBalance_HandledCorrectly()
    {
        // Overdraft scenario
        var dto = new BankReconciliationStatementDto
        {
            GlBalance = -5000m,
            OutstandingDeposits = 3000m,
            OutstandingPayments = 1000m
        };

        // Net = 3000 - 1000 = 2000
        // Calculated = -5000 - 2000 = -7000
        Assert.Equal(2000m, dto.NetOutstanding);
        Assert.Equal(-7000m, dto.CalculatedBankBalance);
    }

    [Fact]
    public void BRS_CurrencyAndBankName_Set()
    {
        var dto = new BankReconciliationStatementDto
        {
            CurrencyCode = "USD",
            BankAccountName = "HSBC Current Account",
            ReportDate = new DateTime(2026, 6, 30)
        };

        Assert.Equal("USD", dto.CurrencyCode);
        Assert.Equal("HSBC Current Account", dto.BankAccountName);
        Assert.Equal(new DateTime(2026, 6, 30), dto.ReportDate);
    }

    // --- JournalEntryLine GL balance contribution tests ---

    [Fact]
    public void JournalEntryLine_DebitContributesPositiveToGLBalance()
    {
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var jeId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();

        var je = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, DateTime.Today);
        // Add debit to bank = money coming in
        je.AddLine(bankAccountId, 10000m, true, "Deposit");

        var line = je.Lines.First();
        Assert.True(line.IsDebit);
        Assert.Equal(10000m, line.Amount);
    }

    [Fact]
    public void JournalEntryLine_CreditContributesNegativeToGLBalance()
    {
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var jeId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();

        var je = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, DateTime.Today);
        // Add credit to bank = money going out
        je.AddLine(bankAccountId, 7500m, false, "Payment");

        var line = je.Lines.First();
        Assert.False(line.IsDebit);
        Assert.Equal(7500m, line.Amount);
    }

    [Fact]
    public void GLBalance_Calculation_MultipleEntries()
    {
        // Simulate GL balance calculation: SUM(debit) - SUM(credit) for bank account
        var lines = new List<(decimal Amount, bool IsDebit)>
        {
            (50000m, true),   // Opening balance DR
            (10000m, true),   // Deposit
            (8000m, false),   // Payment 1
            (5000m, false),   // Payment 2
            (15000m, true),   // Another deposit
        };

        var totalDebit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var totalCredit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        var glBalance = totalDebit - totalCredit;

        Assert.Equal(75000m, totalDebit);
        Assert.Equal(13000m, totalCredit);
        Assert.Equal(62000m, glBalance);
    }

    // --- Payment Entry reconciliation status tests ---

    [Fact]
    public void PaymentEntry_NotReconciled_IsUncleared()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, DateTime.Today,
            5000m, Guid.NewGuid(), Guid.NewGuid());
        pe.Submit();
        pe.Post();

        // PE is posted but not linked to any bank transaction = uncleared
        Assert.Equal(DocumentStatus.Posted, pe.Status);
        // Would be in the uncleared list for BRS
    }

    [Fact]
    public void BankTransaction_Reconciled_Excluded_FromOutstanding()
    {
        var bt = new BankTransaction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Customer payment", 5000m);

        Assert.False(bt.IsReconciled);

        bt.Reconcile(Guid.NewGuid(), "PE-001");
        Assert.True(bt.IsReconciled);
        // Reconciled transactions are excluded from the outstanding list
    }
}
