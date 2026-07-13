using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class BankAutoMatchTests
{
    [Fact]
    public void BankTransaction_Reconcile_SetsAllFields()
    {
        var tx = new BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Payment from ABC", 5000m);

        var peId = Guid.NewGuid();
        tx.Reconcile(peId, "PE-001");

        tx.IsReconciled.ShouldBeTrue();
        tx.PaymentEntryId.ShouldBe(peId);
        tx.MatchedDocumentRef.ShouldBe("PE-001");
        tx.ReconciledAt.ShouldNotBeNull();
    }

    [Fact]
    public void BankTransaction_Unreconcile_ClearsAllFields()
    {
        var tx = new BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Payment", 1000m);

        tx.Reconcile(Guid.NewGuid(), "PE-002");
        tx.Unreconcile();

        tx.IsReconciled.ShouldBeFalse();
        tx.PaymentEntryId.ShouldBeNull();
        tx.MatchedDocumentRef.ShouldBeNull();
        tx.ReconciledAt.ShouldBeNull();
    }

    [Fact]
    public void BankTransaction_PositiveAmount_IsDeposit()
    {
        var tx = new BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Deposit", 5000m);
        tx.Amount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void BankTransaction_NegativeAmount_IsWithdrawal()
    {
        var tx = new BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Withdrawal", -2500m);
        tx.Amount.ShouldBeLessThan(0);
    }

    [Fact]
    public void BankTransaction_DefaultIsNotReconciled()
    {
        var tx = new BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Test", 100m);
        tx.IsReconciled.ShouldBeFalse();
        tx.PaymentEntryId.ShouldBeNull();
    }
}
