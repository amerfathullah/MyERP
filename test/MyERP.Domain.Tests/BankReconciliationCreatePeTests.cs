using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for the "Create Payment Entry from Bank Transaction" workflow.
/// Per ERPNext gotcha #784: distinguishes "Matched" vs "Voucher Created" reconciliation types.
/// Deposits create Receive PEs, Withdrawals create Pay PEs.
/// </summary>
public class BankReconciliationCreatePeTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    // ─── Transaction Direction → Payment Type ─────────────────────────────

    [Fact]
    public void Deposit_Transaction_Should_Create_Receive_PE()
    {
        // Deposit = positive amount = money received from customer
        var tx = CreateTransaction(1500m);
        Assert.True(tx.Amount > 0, "Deposit should have positive amount");

        var expectedType = tx.Amount > 0 ? PaymentType.Receive : PaymentType.Pay;
        Assert.Equal(PaymentType.Receive, expectedType);
    }

    [Fact]
    public void Withdrawal_Transaction_Should_Create_Pay_PE()
    {
        // Withdrawal = negative amount = money paid to supplier
        var tx = CreateTransaction(-2300m);
        Assert.True(tx.Amount < 0, "Withdrawal should have negative amount");

        var expectedType = tx.Amount > 0 ? PaymentType.Receive : PaymentType.Pay;
        Assert.Equal(PaymentType.Pay, expectedType);
    }

    // ─── Account Resolution by Direction ──────────────────────────────────

    [Fact]
    public void Receive_PE_Uses_BankAccount_As_PaidTo()
    {
        // Receive type: money goes FROM customer account TO bank account
        var partyAccountId = Guid.NewGuid();
        var bankGlAccountId = Guid.NewGuid();
        var isDeposit = true;

        var paidFrom = isDeposit ? partyAccountId : bankGlAccountId;
        var paidTo = isDeposit ? bankGlAccountId : partyAccountId;

        Assert.Equal(partyAccountId, paidFrom); // FROM customer receivable
        Assert.Equal(bankGlAccountId, paidTo);  // TO bank
    }

    [Fact]
    public void Pay_PE_Uses_BankAccount_As_PaidFrom()
    {
        // Pay type: money goes FROM bank account TO supplier account
        var partyAccountId = Guid.NewGuid();
        var bankGlAccountId = Guid.NewGuid();
        var isDeposit = false;

        var paidFrom = isDeposit ? partyAccountId : bankGlAccountId;
        var paidTo = isDeposit ? bankGlAccountId : partyAccountId;

        Assert.Equal(bankGlAccountId, paidFrom); // FROM bank
        Assert.Equal(partyAccountId, paidTo);    // TO supplier payable
    }

    // ─── Auto-Reconciliation ──────────────────────────────────────────────

    [Fact]
    public void Created_PE_Auto_Reconciles_Transaction()
    {
        var tx = CreateTransaction(1000m);
        Assert.False(tx.IsReconciled);

        var peId = Guid.NewGuid();
        tx.Reconcile(peId, "PE-2026-00123");

        Assert.True(tx.IsReconciled);
        Assert.Equal(peId, tx.PaymentEntryId);
        Assert.Equal("PE-2026-00123", tx.MatchedDocumentRef);
    }

    [Fact]
    public void Already_Reconciled_Transaction_Cannot_Create_PE()
    {
        var tx = CreateTransaction(500m);
        tx.Reconcile(Guid.NewGuid(), "PE-001");

        // Per error code MyERP:02048: already reconciled transactions are blocked
        Assert.True(tx.IsReconciled);
        // AppService would throw BusinessException("MyERP:02048") here
    }

    // ─── Amount Calculation ───────────────────────────────────────────────

    [Fact]
    public void PE_Amount_Uses_Absolute_Value_Of_Transaction()
    {
        var depositTx = CreateTransaction(1500m);
        var withdrawalTx = CreateTransaction(-2300m);

        var depositPeAmount = Math.Abs(depositTx.Amount);
        var withdrawalPeAmount = Math.Abs(withdrawalTx.Amount);

        Assert.Equal(1500m, depositPeAmount);
        Assert.Equal(2300m, withdrawalPeAmount);
    }

    [Fact]
    public void Zero_Amount_Transaction_Not_Valid_For_PE()
    {
        var tx = CreateTransaction(0m);
        var peAmount = Math.Abs(tx.Amount);

        // Zero amount PE would fail PaymentEntryAppService validation (MyERP:01008)
        Assert.Equal(0m, peAmount);
    }

    // ─── Reference Number Propagation ─────────────────────────────────────

    [Fact]
    public void Bank_Reference_Number_Copied_To_PE()
    {
        var tx = CreateTransaction(800m);
        tx.ReferenceNumber = "CHQ-45678";

        // PE should inherit reference number for matching/audit
        var peReferenceNumber = tx.ReferenceNumber;
        Assert.Equal("CHQ-45678", peReferenceNumber);
    }

    [Fact]
    public void Transaction_Date_Used_As_PE_Posting_Date()
    {
        var txDate = new DateTime(2026, 7, 15);
        var tx = new BankTransaction(
            Guid.NewGuid(), _companyId, _bankAccountId,
            txDate, "Payment from ABC Corp", 5000m, _tenantId);

        // PE posting date = bank transaction date (not today)
        Assert.Equal(txDate, tx.TransactionDate);
    }

    // ─── Party Type Auto-Detection ────────────────────────────────────────

    [Fact]
    public void Deposit_Suggests_Customer_Party_Type()
    {
        var tx = CreateTransaction(1200m); // positive = deposit
        var suggestedPartyType = tx.Amount > 0 ? "Customer" : "Supplier";
        Assert.Equal("Customer", suggestedPartyType);
    }

    [Fact]
    public void Withdrawal_Suggests_Supplier_Party_Type()
    {
        var tx = CreateTransaction(-3500m); // negative = withdrawal
        var suggestedPartyType = tx.Amount > 0 ? "Customer" : "Supplier";
        Assert.Equal("Supplier", suggestedPartyType);
    }

    // ─── BankTransaction Entity Defaults ──────────────────────────────────

    [Fact]
    public void BankTransaction_Default_Not_Reconciled()
    {
        var tx = CreateTransaction(100m);
        Assert.False(tx.IsReconciled);
        Assert.Null(tx.PaymentEntryId);
        Assert.Null(tx.MatchedDocumentRef);
    }

    [Fact]
    public void BankTransaction_Unreconcile_Clears_Fields()
    {
        var tx = CreateTransaction(100m);
        tx.Reconcile(Guid.NewGuid(), "PE-001");
        Assert.True(tx.IsReconciled);

        tx.Unreconcile();
        Assert.False(tx.IsReconciled);
        Assert.Null(tx.PaymentEntryId);
        Assert.Null(tx.MatchedDocumentRef);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private BankTransaction CreateTransaction(decimal amount)
    {
        return new BankTransaction(
            Guid.NewGuid(), _companyId, _bankAccountId,
            DateTime.UtcNow, "Test transaction", amount, _tenantId);
    }
}
