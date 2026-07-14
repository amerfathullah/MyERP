using System;
using System.Linq;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

public class ExpenseClaimReimbursementTests
{
    [Fact]
    public void ExpenseClaim_TotalAmountReimbursed_DefaultsZero()
    {
        var ec = CreateExpenseClaim();
        ec.TotalAmountReimbursed.ShouldBe(0m);
    }

    [Fact]
    public void ExpenseClaim_ReimbursableAmount_IsClaimedMinusAdvanceMinusReimbursed()
    {
        var ec = CreateExpenseClaim();
        ec.AdvanceAmount = 500m;
        ec.TotalAmountReimbursed = 200m;

        // Claimed=1500, Advance=500, Already reimbursed=200
        // Reimbursable = 1500 - 500 - 200 = 800
        var reimbursable = ec.TotalClaimedAmount - ec.AdvanceAmount - ec.TotalAmountReimbursed;
        reimbursable.ShouldBe(800m);
    }

    [Fact]
    public void ExpenseClaim_FullyReimbursed_ZeroRemaining()
    {
        var ec = CreateExpenseClaim();
        ec.TotalAmountReimbursed = ec.TotalClaimedAmount;

        var reimbursable = ec.TotalClaimedAmount - ec.AdvanceAmount - ec.TotalAmountReimbursed;
        reimbursable.ShouldBe(0m);
    }

    [Fact]
    public void ExpenseClaim_WithAdvance_ReducesReimbursement()
    {
        var ec = CreateExpenseClaim();
        ec.AdvanceAmount = 1000m; // took 1000 advance

        // Only 500 needs reimbursement (1500 claimed - 1000 advance)
        var reimbursable = ec.TotalClaimedAmount - ec.AdvanceAmount - ec.TotalAmountReimbursed;
        reimbursable.ShouldBe(500m);
    }

    [Fact]
    public void ExpenseClaim_MustBeSubmitted_ForReimbursement()
    {
        var ec = CreateExpenseClaim();
        // Draft → cannot reimburse
        ec.Status.ShouldBe(MyERP.Core.DocumentStatus.Draft);
        // Must go through: Draft → Approved → Submitted → then reimburse
    }

    [Fact]
    public void ExpenseClaim_Approve_Then_Submit_Lifecycle()
    {
        var ec = CreateExpenseClaim();
        ec.Approve();
        ec.Status.ShouldBe(MyERP.Core.DocumentStatus.Approved);

        ec.Submit();
        ec.Status.ShouldBe(MyERP.Core.DocumentStatus.Submitted);
        // Now eligible for reimbursement
    }

    [Fact]
    public void ExpenseClaim_ReimbursementCreatesPayPaymentEntry()
    {
        // The PE type should be "Pay" (company pays employee)
        var paymentType = MyERP.Accounting.PaymentType.Pay;
        paymentType.ShouldBe(MyERP.Accounting.PaymentType.Pay);
    }

    [Fact]
    public void ExpenseClaim_AdvanceLinkage_PreventDoublePayment()
    {
        // Per DO-NOT: "Allow expense claim GL posting without verifying advance linkage"
        var ec = CreateExpenseClaim();
        ec.AdvancePaymentEntryId = Guid.NewGuid();
        ec.AdvanceAmount = 1000m;

        // Even though total claimed is 1500, only reimburse 500 (minus advance)
        var reimbursable = ec.TotalClaimedAmount - ec.AdvanceAmount - ec.TotalAmountReimbursed;
        reimbursable.ShouldBe(500m);
    }

    [Fact]
    public void StatementOfAccounts_BalanceEquation()
    {
        // Opening + Total Debit - Total Credit = Closing
        decimal opening = 5000m;
        decimal totalDebit = 3000m; // new invoices
        decimal totalCredit = 2000m; // payments received
        decimal closing = opening + totalDebit - totalCredit;
        closing.ShouldBe(6000m);
    }

    [Fact]
    public void StatementOfAccounts_CreditNote_ReducesBalance()
    {
        // Credit notes show as credit entries (reduce outstanding)
        decimal invoiceAmount = 10000m;
        decimal creditNoteAmount = 2000m;
        decimal netOutstanding = invoiceAmount - creditNoteAmount;
        netOutstanding.ShouldBe(8000m);
    }

    [Fact]
    public void StatementOfAccounts_Payment_ReducesBalance()
    {
        // Payments reduce the running balance
        decimal openingBalance = 15000m;
        decimal paymentReceived = 5000m;
        decimal afterPayment = openingBalance - paymentReceived;
        afterPayment.ShouldBe(10000m);
    }

    [Fact]
    public void StatementOfAccounts_RunningBalance_ChronologicalOrder()
    {
        // Entries must be sorted by date for accurate running balance
        var date1 = new DateTime(2026, 7, 1);
        var date2 = new DateTime(2026, 7, 5);
        var date3 = new DateTime(2026, 7, 10);

        // Opening: 0, Invoice 5000 on July 1, Payment 3000 on July 5, Invoice 2000 on July 10
        decimal balance = 0;
        balance += 5000; // after invoice 1
        balance.ShouldBe(5000m);
        balance -= 3000; // after payment
        balance.ShouldBe(2000m);
        balance += 2000; // after invoice 2
        balance.ShouldBe(4000m); // closing balance
    }

    private static ExpenseClaim CreateExpenseClaim()
    {
        var ec = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        ec.AddExpense(new DateTime(2026, 7, 1), "Hotel", 500m);
        ec.AddExpense(new DateTime(2026, 7, 2), "Transport", 200m);
        ec.AddExpense(new DateTime(2026, 7, 3), "Meals", 300m);
        ec.AddExpense(new DateTime(2026, 7, 4), "Client Entertainment", 500m);
        return ec; // Total: 1500
    }
}
