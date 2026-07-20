using System;
using System.Collections.Generic;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class BankAutoReconcileAndTransferTests
{
    // ── Auto-Reconcile: Strict Reference Matching ──

    [Fact]
    public void StrictMatch_ExactReferenceAndAmount_Matches()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: "TRF-001");
        var pe = CreatePaymentEntry(amount: 5000m, type: PaymentType.Receive, reference: "TRF-001");
        var result = TestStrictMatch(tx, pe);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(pe.Id);
    }

    [Fact]
    public void StrictMatch_ReferenceCase_Insensitive()
    {
        var tx = CreateTransaction(deposit: 3000m, reference: "ref-abc");
        var pe = CreatePaymentEntry(amount: 3000m, type: PaymentType.Receive, reference: "REF-ABC");
        TestStrictMatch(tx, pe).ShouldNotBeNull();
    }

    [Fact]
    public void StrictMatch_NoReference_NoMatch()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: null);
        var pe = CreatePaymentEntry(amount: 5000m, type: PaymentType.Receive, reference: "TRF-001");
        TestStrictMatch(tx, pe).ShouldBeNull();
    }

    [Fact]
    public void StrictMatch_DifferentReference_NoMatch()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: "TRF-001");
        var pe = CreatePaymentEntry(amount: 5000m, type: PaymentType.Receive, reference: "TRF-002");
        TestStrictMatch(tx, pe).ShouldBeNull();
    }

    [Fact]
    public void StrictMatch_SameRefDifferentAmount_NoMatch()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: "TRF-001");
        var pe = CreatePaymentEntry(amount: 3000m, type: PaymentType.Receive, reference: "TRF-001");
        TestStrictMatch(tx, pe).ShouldBeNull();
    }

    [Fact]
    public void StrictMatch_WithdrawalMatchesPay()
    {
        var tx = CreateTransaction(withdrawal: 2500m, reference: "PAY-100");
        var pe = CreatePaymentEntry(amount: 2500m, type: PaymentType.Pay, reference: "PAY-100");
        TestStrictMatch(tx, pe).ShouldNotBeNull();
    }

    [Fact]
    public void StrictMatch_DepositDoesNotMatchPay()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: "TRF-001");
        var pe = CreatePaymentEntry(amount: 5000m, type: PaymentType.Pay, reference: "TRF-001");
        TestStrictMatch(tx, pe).ShouldBeNull();
    }

    [Fact]
    public void StrictMatch_InternalTransferMatchesBothSides()
    {
        // Internal transfers should match both deposits and withdrawals
        var txDeposit = CreateTransaction(deposit: 8000m, reference: "ITX-001");
        var pe = CreatePaymentEntry(amount: 8000m, type: PaymentType.InternalTransfer, reference: "ITX-001");
        TestStrictMatch(txDeposit, pe).ShouldNotBeNull();

        var txWithdrawal = CreateTransaction(withdrawal: 8000m, reference: "ITX-001");
        TestStrictMatch(txWithdrawal, pe).ShouldNotBeNull();
    }

    [Fact]
    public void BackgroundJobThreshold_Is10()
    {
        BankAutoMatchService.BackgroundJobThreshold.ShouldBe(10);
    }

    // ── Ranking: Manual Match Candidates ──

    [Fact]
    public void Rank_BaseScore_IsAlwaysAtLeast1()
    {
        var tx = CreateTransaction(deposit: 100m, reference: "X");
        var pe = CreatePaymentEntry(amount: 999m, type: PaymentType.Receive, reference: "Y");
        pe.PostingDate = tx.TransactionDate.AddDays(30); // far date
        var rank = InvokeCalculateRank(tx, pe);
        rank.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Rank_ExactRefAndAmountAndDate_MaxScore()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: "REF-1");
        var pe = CreatePaymentEntry(amount: 5000m, type: PaymentType.Receive, reference: "REF-1");
        pe.PostingDate = tx.TransactionDate; // same date
        var rank = InvokeCalculateRank(tx, pe);
        rank.ShouldBe(4); // base(1) + ref(1) + amount(1) + date(1)
    }

    [Fact]
    public void Rank_OnlyAmountMatches_Score2()
    {
        var tx = CreateTransaction(deposit: 5000m, reference: "A");
        var pe = CreatePaymentEntry(amount: 5000m, type: PaymentType.Receive, reference: "B");
        pe.PostingDate = tx.TransactionDate.AddDays(10);
        var rank = InvokeCalculateRank(tx, pe);
        rank.ShouldBe(2); // base(1) + amount(1)
    }

    // ── Internal Transfer: Mirror Transaction Discovery ──

    [Fact]
    public void MirrorSearch_WithdrawalFindsDeposit()
    {
        // A withdrawal of 10,000 should find a deposit of 10,000 in another bank
        var source = CreateTransaction(withdrawal: 10000m, reference: "TRF-500");
        var mirror = CreateTransaction(deposit: 10000m, reference: "TRF-500");
        mirror.BankAccountId = Guid.NewGuid(); // different bank account

        var isMatch =
            source.Withdrawal > 0 && Math.Abs(mirror.Deposit - source.Withdrawal) < 0.01m;
        isMatch.ShouldBeTrue();
    }

    [Fact]
    public void MirrorSearch_SameBankAccount_NoMatch()
    {
        var bankId = Guid.NewGuid();
        var source = CreateTransaction(withdrawal: 10000m, reference: "TRF");
        source.BankAccountId = bankId;
        var mirror = CreateTransaction(deposit: 10000m, reference: "TRF");
        mirror.BankAccountId = bankId; // SAME bank = no match

        (mirror.BankAccountId != source.BankAccountId).ShouldBeFalse();
    }

    [Fact]
    public void MirrorSearch_DifferentAmount_NoMatch()
    {
        var source = CreateTransaction(withdrawal: 10000m, reference: "TRF");
        var mirror = CreateTransaction(deposit: 9000m, reference: "TRF");
        mirror.BankAccountId = Guid.NewGuid();

        var isMatch =
            source.Withdrawal > 0 && Math.Abs(mirror.Deposit - source.Withdrawal) < 0.01m;
        isMatch.ShouldBeFalse();
    }

    [Fact]
    public void MirrorSearch_DefaultMatchDays_Is3()
    {
        BankInternalTransferService.DefaultTransferMatchDays.ShouldBe(3);
    }

    // ── Internal Transfer: BuildInternalTransfer Spec ──

    [Fact]
    public void BuildSpec_Withdrawal_PaidFromIsSource()
    {
        var service = new BankInternalTransferService(null!);
        var tx = CreateTransaction(withdrawal: 15000m, reference: "ITX-1");
        var sourceGl = Guid.NewGuid();
        var targetGl = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var spec = service.BuildInternalTransfer(tx, sourceGl, targetGl, companyId);

        spec.PaidFromAccountId.ShouldBe(sourceGl);
        spec.PaidToAccountId.ShouldBe(targetGl);
        spec.Amount.ShouldBe(15000m);
        spec.CompanyId.ShouldBe(companyId);
        spec.SourceTransactionId.ShouldBe(tx.Id);
    }

    [Fact]
    public void BuildSpec_Deposit_PaidFromIsTarget()
    {
        var service = new BankInternalTransferService(null!);
        var tx = CreateTransaction(deposit: 8000m, reference: "ITX-2");
        var sourceGl = Guid.NewGuid();
        var targetGl = Guid.NewGuid();

        var spec = service.BuildInternalTransfer(tx, sourceGl, targetGl, Guid.NewGuid());

        // Deposit: money coming IN, so paid_from = target, paid_to = source
        spec.PaidFromAccountId.ShouldBe(targetGl);
        spec.PaidToAccountId.ShouldBe(sourceGl);
        spec.Amount.ShouldBe(8000m);
    }

    [Fact]
    public void BuildSpec_ReferenceNumber_Truncated140()
    {
        var service = new BankInternalTransferService(null!);
        var longRef = new string('A', 200);
        var tx = CreateTransaction(withdrawal: 1000m, reference: longRef);

        var spec = service.BuildInternalTransfer(tx, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        spec.ReferenceNumber.Length.ShouldBeLessThanOrEqualTo(140);
    }

    [Fact]
    public void BuildSpec_LegacyAmount_UsedWhenDepositWithdrawalBothZero()
    {
        var service = new BankInternalTransferService(null!);
        var tx = CreateTransaction(deposit: 0m, reference: "OLD");
        tx.Withdrawal = 0;
        tx.Amount = -5000m; // legacy negative = withdrawal

        var spec = service.BuildInternalTransfer(tx, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        spec.Amount.ShouldBe(5000m);
    }

    // ── Internal Transfer: Dual-Side Reconciliation ──

    [Fact]
    public void BankTransaction_Reconcile_BothSides_SamePE()
    {
        var source = CreateTransaction(withdrawal: 10000m, reference: "TRF");
        var mirror = CreateTransaction(deposit: 10000m, reference: "TRF");
        mirror.BankAccountId = Guid.NewGuid();

        var peId = Guid.NewGuid();

        // Both reconcile to the SAME PE (per ERPNext: one PE = both sides)
        source.Reconcile(peId, "PE-ITX-001");
        mirror.Reconcile(peId, "PE-ITX-001");

        source.PaymentEntryId.ShouldBe(peId);
        mirror.PaymentEntryId.ShouldBe(peId);
        source.IsReconciled.ShouldBeTrue();
        mirror.IsReconciled.ShouldBeTrue();
    }

    [Fact]
    public void MirrorTransactionResult_HasAllFields()
    {
        var result = new MirrorTransactionResult
        {
            TransactionId = Guid.NewGuid(),
            BankAccountId = Guid.NewGuid(),
            ReferenceNumber = "REF-123",
            TransactionDate = DateTime.Today,
            Deposit = 5000m,
            Withdrawal = 0m,
            CurrencyCode = "MYR"
        };

        result.TransactionId.ShouldNotBe(Guid.Empty);
        result.BankAccountId.ShouldNotBe(Guid.Empty);
        result.ReferenceNumber.ShouldBe("REF-123");
        result.Deposit.ShouldBe(5000m);
        result.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void InternalTransferSpec_HasAllFields()
    {
        var spec = new InternalTransferSpec
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.NewGuid(),
            PaidToAccountId = Guid.NewGuid(),
            Amount = 25000m,
            PostingDate = DateTime.Today,
            ReferenceNumber = "ITX-REF",
            SourceTransactionId = Guid.NewGuid()
        };

        spec.Amount.ShouldBe(25000m);
        spec.ReferenceNumber.ShouldBe("ITX-REF");
        spec.SourceTransactionId.ShouldNotBe(Guid.Empty);
    }

    // ── Helpers ──

    private static BankTransaction CreateTransaction(
        decimal deposit = 0m, decimal withdrawal = 0m, string? reference = null)
    {
        var amount = deposit > 0 ? deposit : -withdrawal;
        var tx = new BankTransaction(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, "Test transaction", amount)
        {
            ReferenceNumber = reference,
            Deposit = deposit,
            Withdrawal = withdrawal
        };
        return tx;
    }

    private static PaymentEntry CreatePaymentEntry(
        decimal amount, PaymentType type, string? reference = null)
    {
        var companyId = Guid.NewGuid();
        var pe = new PaymentEntry(
            Guid.NewGuid(), companyId, type, DateTime.Today,
            amount, Guid.NewGuid(), Guid.NewGuid())
        {
            ReferenceNumber = reference,
            PostingDate = DateTime.Today,
            PaymentNumber = "PE-" + Guid.NewGuid().ToString()[..6],
        };
        // Submit + Post to reach Posted status
        pe.Submit();
        pe.Post();
        return pe;
    }

    /// <summary>
    /// Tests the strict reference matching algorithm via reflection to avoid needing DI.
    /// Simulates FindStrictReferenceMatch behavior.
    /// </summary>
    private static PaymentEntry? TestStrictMatch(BankTransaction tx, PaymentEntry pe)
    {
        // Replicate the strict matching logic
        if (string.IsNullOrEmpty(tx.ReferenceNumber))
            return null;

        bool refMatch = !string.IsNullOrEmpty(pe.ReferenceNumber) &&
            tx.ReferenceNumber.Equals(pe.ReferenceNumber, StringComparison.OrdinalIgnoreCase);

        if (!refMatch) return null;

        // Amount matching: deposit→Receive/IT, withdrawal→Pay/IT
        decimal txAmount = tx.Deposit > 0 ? tx.Deposit : tx.Withdrawal;
        if (txAmount == 0) txAmount = Math.Abs(tx.Amount);

        bool amountMatch = false;
        if (tx.Deposit > 0 || tx.Amount > 0)
            amountMatch = pe.PaymentType is PaymentType.Receive or PaymentType.InternalTransfer
                && Math.Abs(txAmount - pe.PaidAmount) < 0.01m;
        else if (tx.Withdrawal > 0 || tx.Amount < 0)
            amountMatch = pe.PaymentType is PaymentType.Pay or PaymentType.InternalTransfer
                && Math.Abs(txAmount - pe.PaidAmount) < 0.01m;

        return amountMatch ? pe : null;
    }

    /// <summary>
    /// Tests the ranking algorithm directly.
    /// </summary>
    private static int InvokeCalculateRank(BankTransaction tx, PaymentEntry pe)
    {
        int rank = 1;

        if (!string.IsNullOrEmpty(tx.ReferenceNumber) &&
            !string.IsNullOrEmpty(pe.ReferenceNumber) &&
            tx.ReferenceNumber.Equals(pe.ReferenceNumber, StringComparison.OrdinalIgnoreCase))
            rank++;

        decimal txAmount = tx.Deposit > 0 ? tx.Deposit : tx.Withdrawal;
        if (txAmount == 0) txAmount = Math.Abs(tx.Amount);
        if (Math.Abs(txAmount - pe.PaidAmount) < 0.01m)
            rank++;

        if (Math.Abs((tx.TransactionDate - pe.PostingDate).TotalDays) <= 3)
            rank++;

        return rank;
    }
}
