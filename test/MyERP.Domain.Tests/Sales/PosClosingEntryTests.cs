using System;
using System.Collections.Generic;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class PosClosingEntryTests
{
    [Fact]
    public void Create_SetsDefaultStatus()
    {
        var entry = CreateEntry();
        entry.Status.ShouldBe(PosClosingStatus.Draft);
    }

    [Fact]
    public void Create_SetsPostingDateToNow()
    {
        var entry = CreateEntry();
        entry.PostingDate.ShouldBe(DateTime.UtcNow.Date);
    }

    [Fact]
    public void AddPayment_InDraft_Succeeds()
    {
        var entry = CreateEntry();
        entry.AddPayment(Guid.NewGuid(), "Cash", 1000, 990);

        entry.Payments.Count.ShouldBe(1);
        entry.Payments[0].ExpectedAmount.ShouldBe(1000m);
        entry.Payments[0].ClosingAmount.ShouldBe(990m);
        entry.Payments[0].Difference.ShouldBe(10m); // Short by 10
    }

    [Fact]
    public void AddInvoice_InDraft_Succeeds()
    {
        var entry = CreateEntry();
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500);

        entry.Invoices.Count.ShouldBe(1);
    }

    [Fact]
    public void Submit_WithInvoices_CalculatesGrandTotal()
    {
        var entry = CreateEntry();
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500);
        entry.AddInvoice(Guid.NewGuid(), "POS-002", 300);

        entry.Submit();

        entry.Status.ShouldBe(PosClosingStatus.Submitted);
        entry.GrandTotal.ShouldBe(800m);
    }

    [Fact]
    public void Submit_WithoutInvoices_Throws()
    {
        var entry = CreateEntry();
        Should.Throw<BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void Cancel_FromSubmitted_Succeeds()
    {
        var entry = CreateEntry();
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500);
        entry.Submit();

        entry.Cancel();
        entry.Status.ShouldBe(PosClosingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_Throws()
    {
        var entry = CreateEntry();
        Should.Throw<BusinessException>(() => entry.Cancel());
    }

    [Fact]
    public void TotalDifference_SumsPaymentVariances()
    {
        var entry = CreateEntry();
        entry.AddPayment(Guid.NewGuid(), "Cash", 1000, 990);     // +10 short
        entry.AddPayment(Guid.NewGuid(), "Card", 500, 510);      // -10 over
        entry.AddPayment(Guid.NewGuid(), "E-Wallet", 200, 200);  // 0 exact

        entry.TotalDifference.ShouldBe(0m); // net zero
    }

    [Fact]
    public void AddPayment_AfterSubmit_Throws()
    {
        var entry = CreateEntry();
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500);
        entry.Submit();

        Should.Throw<BusinessException>(() =>
            entry.AddPayment(Guid.NewGuid(), "Cash", 100, 100));
    }

    [Fact]
    public void AddInvoice_AfterSubmit_Throws()
    {
        var entry = CreateEntry();
        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500);
        entry.Submit();

        Should.Throw<BusinessException>(() =>
            entry.AddInvoice(Guid.NewGuid(), "POS-002", 300));
    }

    [Fact]
    public void GetChangePaymentRow_PrefersCashRowOnMatchingAccount()
    {
        var changeAccountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var mopId = Guid.NewGuid();

        var rows = new List<PosClosingAppService.PosPaymentRow>
        {
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = otherAccountId, Type = "Cash", Amount = 100 },
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = changeAccountId, Type = "Bank", Amount = 100 },
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = changeAccountId, Type = "Cash", Amount = 100 },
        };

        var target = PosClosingAppService.GetChangePaymentRow(rows, changeAccountId);

        target.ShouldNotBeNull();
        target.AccountId.ShouldBe(changeAccountId);
        target.Type.ShouldBe("Cash");
        target.ShouldBe(rows[2]);
    }

    [Fact]
    public void GetChangePaymentRow_FallsBackToAnyRowOnMatchingAccount()
    {
        var changeAccountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var mopId = Guid.NewGuid();

        var rows = new List<PosClosingAppService.PosPaymentRow>
        {
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = otherAccountId, Type = "Bank", Amount = 100 },
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = changeAccountId, Type = "Bank", Amount = 100 },
        };

        var target = PosClosingAppService.GetChangePaymentRow(rows, changeAccountId);

        target.ShouldNotBeNull();
        target.AccountId.ShouldBe(changeAccountId);
        target.ShouldBe(rows[1]);
    }

    [Fact]
    public void GetChangePaymentRow_FallsBackToAnyCashRow_WhenNoAccountMatch()
    {
        var changeAccountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var mopId = Guid.NewGuid();

        var rows = new List<PosClosingAppService.PosPaymentRow>
        {
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = otherAccountId, Type = "Bank", Amount = 50 },
            new() { InvoiceId = Guid.NewGuid(), ModeOfPaymentId = mopId, AccountId = otherAccountId, Type = "Cash", Amount = 100 },
        };

        var target = PosClosingAppService.GetChangePaymentRow(rows, changeAccountId);

        target.ShouldNotBeNull();
        target.Type.ShouldBe("Cash");
        target.ShouldBe(rows[1]);
    }

    [Fact]
    public void AggregatePaymentsWithChangeDeduction_DeductsFromSingleRowPerInvoice()
    {
        var invoice1 = Guid.NewGuid();
        var invoice2 = Guid.NewGuid();
        var cashMop = Guid.NewGuid();
        var cardMop = Guid.NewGuid();
        var cashAccount = Guid.NewGuid();

        var paymentRows = new List<PosClosingAppService.PosPaymentRow>
        {
            new() { InvoiceId = invoice1, ModeOfPaymentId = cashMop, AccountId = cashAccount, Type = "Cash", Amount = 100 },
            new() { InvoiceId = invoice1, ModeOfPaymentId = cardMop, AccountId = Guid.NewGuid(), Type = "Bank", Amount = 50 },
            new() { InvoiceId = invoice2, ModeOfPaymentId = cashMop, AccountId = cashAccount, Type = "Cash", Amount = 80 },
        };

        var changes = new List<(Guid InvoiceId, decimal ChangeAmount, Guid? ChangeAccountId)>
        {
            (invoice1, 20m, cashAccount), // 100 Cash - 20 = 80 Cash; Card remains 50
            (invoice2, 10m, cashAccount), // 80 Cash - 10 = 70 Cash
        };

        var result = PosClosingAppService.AggregatePaymentsWithChangeDeduction(paymentRows, changes);

        result[cashMop].ShouldBe(150m); // 80 + 70 = 150
        result[cardMop].ShouldBe(50m);  // Card untouched
    }

    private static PosClosingEntry CreateEntry()
    {
        return new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
    }
}
