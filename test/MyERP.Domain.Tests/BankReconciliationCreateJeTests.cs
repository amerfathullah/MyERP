using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for the "Create Journal Entry from Bank Transaction" workflow.
/// Migrated from ERPNext bank_reconciliation_tool.py: create_journal_entry_bts() (PR #57330).
/// Verifies double-entry balance, debit/credit direction for deposits/withdrawals,
/// auto-reconciliation linkage, and bank clearance date propagation.
/// </summary>
public class BankReconciliationCreateJeTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _bankGlAccountId = Guid.NewGuid();
    private readonly Guid _chargesAccountId = Guid.NewGuid();
    private readonly Guid _interestAccountId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Deposit_Transaction_Should_Debit_Bank_And_Credit_Second_Account()
    {
        var tx = CreateTransaction(500m); // deposit
        var amount = tx.Amount; // 500
        var isDeposit = tx.Amount > 0;

        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, tx.TransactionDate, _tenantId)
        {
            EntryNumber = "JE-2026-0001",
            VoucherType = JournalEntryVoucherType.BankEntry,
            ReferenceType = "BankTransaction",
            ReferenceId = tx.Id,
            ReferenceNumber = tx.ReferenceNumber,
            Narration = tx.Description,
        };

        // Bank line: Debit on deposit
        je.AddFullLine(_bankGlAccountId, amount, isDebit: isDeposit, description: tx.Description);
        // Second line (Interest): Credit on deposit
        je.AddFullLine(_interestAccountId, amount, isDebit: !isDeposit, description: "Bank interest income");

        je.Validate();
        je.Post();
        je.SetClearanceDate(tx.TransactionDate);

        je.Status.ShouldBe(DocumentStatus.Posted);
        je.TotalDebit.ShouldBe(500m);
        je.TotalCredit.ShouldBe(500m);
        je.ClearanceDate.ShouldBe(tx.TransactionDate);

        var bankLine = je.Lines.First(l => l.AccountId == _bankGlAccountId);
        bankLine.IsDebit.ShouldBeTrue();
        bankLine.Amount.ShouldBe(500m);

        var secondLine = je.Lines.First(l => l.AccountId == _interestAccountId);
        secondLine.IsDebit.ShouldBeFalse();
        secondLine.Amount.ShouldBe(500m);

        // Auto-reconcile bank transaction with journal entry
        tx.ReconcileWithJournalEntry(je.Id, je.EntryNumber);
        tx.IsReconciled.ShouldBeTrue();
        tx.JournalEntryId.ShouldBe(je.Id);
        tx.PaymentEntryId.ShouldBeNull();
        tx.MatchedDocumentRef.ShouldBe("JE-2026-0001");
    }

    [Fact]
    public void Withdrawal_Transaction_Should_Credit_Bank_And_Debit_Second_Account()
    {
        var tx = CreateTransaction(-25m); // withdrawal (e.g. bank charges)
        var amount = Math.Abs(tx.Amount); // 25
        var isDeposit = tx.Amount > 0;

        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, tx.TransactionDate, _tenantId)
        {
            EntryNumber = "JE-2026-0002",
            VoucherType = JournalEntryVoucherType.BankEntry,
            ReferenceType = "BankTransaction",
            ReferenceId = tx.Id,
            ReferenceNumber = tx.ReferenceNumber,
            Narration = tx.Description,
        };

        var costCenterId = Guid.NewGuid();

        // Bank line: Credit on withdrawal
        je.AddFullLine(_bankGlAccountId, amount, isDebit: isDeposit, description: tx.Description);
        // Second line (Bank charges): Debit on withdrawal
        je.AddFullLine(_chargesAccountId, amount, isDebit: !isDeposit, description: "Bank service fee", costCenterId: costCenterId);

        je.Validate();
        je.Post();
        je.SetClearanceDate(tx.TransactionDate);

        je.Status.ShouldBe(DocumentStatus.Posted);
        je.TotalDebit.ShouldBe(25m);
        je.TotalCredit.ShouldBe(25m);
        je.ClearanceDate.ShouldBe(tx.TransactionDate);

        var bankLine = je.Lines.First(l => l.AccountId == _bankGlAccountId);
        bankLine.IsDebit.ShouldBeFalse();
        bankLine.Amount.ShouldBe(25m);

        var secondLine = je.Lines.First(l => l.AccountId == _chargesAccountId);
        secondLine.IsDebit.ShouldBeTrue();
        secondLine.Amount.ShouldBe(25m);
        secondLine.CostCenterId.ShouldBe(costCenterId);

        tx.ReconcileWithJournalEntry(je.Id, je.EntryNumber);
        tx.IsReconciled.ShouldBeTrue();
        tx.JournalEntryId.ShouldBe(je.Id);
    }

    [Fact]
    public void Reconciled_Transaction_Cannot_Be_Reconciled_Again_Without_Unreconcile()
    {
        var tx = CreateTransaction(100m);
        var jeId = Guid.NewGuid();
        tx.ReconcileWithJournalEntry(jeId, "JE-001");

        tx.IsReconciled.ShouldBeTrue();
        tx.JournalEntryId.ShouldBe(jeId);

        // Unreconcile restores state
        tx.Unreconcile();
        tx.IsReconciled.ShouldBeFalse();
        tx.JournalEntryId.ShouldBeNull();
        tx.MatchedDocumentRef.ShouldBeNull();
    }

    [Fact]
    public void AddFullLine_Supports_Party_Reference()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow, _tenantId);
        var partyId = Guid.NewGuid();
        var partyType = "Supplier";
        var costCenterId = Guid.NewGuid();

        je.AddFullLine(_chargesAccountId, 150m, isDebit: true, "Supplier fee", partyId, partyType, costCenterId);
        je.AddFullLine(_bankGlAccountId, 150m, isDebit: false, "Paid from bank");

        je.Validate();
        je.Lines.Count.ShouldBe(2);

        var line = je.Lines.First(l => l.AccountId == _chargesAccountId);
        line.PartyId.ShouldBe(partyId);
        line.PartyType.ShouldBe(partyType);
        line.CostCenterId.ShouldBe(costCenterId);
    }

    private BankTransaction CreateTransaction(decimal amount)
    {
        return new BankTransaction(
            Guid.NewGuid(),
            _companyId,
            _bankAccountId,
            DateTime.UtcNow.Date,
            amount > 0 ? "Interest deposit" : "Monthly bank charges",
            amount,
            _tenantId)
        {
            ReferenceNumber = "REF-" + Guid.NewGuid().ToString("N")[..8],
            Deposit = amount > 0 ? amount : 0,
            Withdrawal = amount < 0 ? Math.Abs(amount) : 0,
            CurrencyCode = "MYR",
        };
    }
}
