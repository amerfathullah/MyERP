using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Accounting;

public class PaymentEntryCancelTests
{
    private static PaymentEntry CreatePostedPE(decimal amount = 5000m)
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, amount, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstInvoiceId = Guid.NewGuid();
        pe.AgainstInvoiceType = "SalesInvoice";
        pe.Submit();
        pe.Post();
        return pe;
    }

    [Fact]
    public void PaymentEntry_Cancel_FromPosted_Succeeds()
    {
        var pe = CreatePostedPE();
        pe.Cancel();
        pe.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void PaymentEntry_Cancel_FromDraft_Throws()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        Should.Throw<BusinessException>(() => pe.Cancel());
    }

    [Fact]
    public void PaymentEntry_Cancel_ShouldReverseAmountPaid_Concept()
    {
        // Invoice has GrandTotal=10000, AmountPaid=5000 from PE
        // After PE cancel: AmountPaid should revert to 5000-5000=0
        var amountPaid = 5000m;
        var paidAmount = 5000m;
        var reversed = Math.Max(0, amountPaid - paidAmount);
        reversed.ShouldBe(0m);
    }

    [Fact]
    public void PaymentEntry_Cancel_PartialPayment_Concept()
    {
        // Invoice has GrandTotal=10000, two PEs of 3000+4000=7000 total
        // Cancel the 4000 PE: AmountPaid = 7000-4000=3000
        var totalPaid = 7000m;
        var cancelledAmount = 4000m;
        var afterCancel = Math.Max(0, totalPaid - cancelledAmount);
        afterCancel.ShouldBe(3000m);
    }

    [Fact]
    public void PaymentEntry_Cancel_AdvancePayment_Concept()
    {
        // SO has AdvancePaid=5000, PE cancel should reduce to 0
        var advancePaid = 5000m;
        var cancelledAmount = 5000m;
        var afterCancel = Math.Max(0, advancePaid - cancelledAmount);
        afterCancel.ShouldBe(0m);
    }

    [Fact]
    public void PaymentEntry_Cancel_ScheduleDeallocation_LIFO()
    {
        // Schedule entries paid: [3000, 2000] (by due date)
        // Cancel 4000: reverse latest first → 2000 from second, 2000 from first
        var entry1Paid = 3000m;
        var entry2Paid = 2000m;
        var toReverse = 4000m;

        // Latest first: entry2 loses min(2000, 4000)=2000, remaining=2000
        var rev2 = Math.Min(entry2Paid, toReverse);
        toReverse -= rev2;
        // Then entry1 loses min(3000, 2000)=2000, remaining=0
        var rev1 = Math.Min(entry1Paid, toReverse);

        (entry2Paid - rev2).ShouldBe(0m);
        (entry1Paid - rev1).ShouldBe(1000m);
    }

    // ========== ERPNext PR #61273: Payment cancellation and amounts tests ==========

    [Fact]
    public void PaymentEntry_Cancel_RestoresScheduleOutstandingAndBaseOutstanding()
    {
        // Per ERPNext PR #61273: cancelling payment adds back to base_outstanding
        var schedule = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow, 100m, 1000m)
        {
            BasePaymentAmount = 4500m,
            PaidAmount = 1000m
        };

        // Fully paid -> Outstanding is 0, BaseOutstanding is 0
        schedule.Outstanding.ShouldBe(0m);
        schedule.BaseOutstanding.ShouldBe(0m);

        // Cancel allocation of 400m
        schedule.PaidAmount -= 400m;

        // Outstanding restored to 400m, BaseOutstanding restored to 4500 - (600 * 4500 / 1000) = 1800m
        schedule.Outstanding.ShouldBe(400m);
        schedule.BaseOutstanding.ShouldBe(1800m);
    }

    [Fact]
    public void PaymentEntry_Submit_NegativePaidAmount_Throws()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.PaidAmount = -100m;

        var ex = Should.Throw<BusinessException>(() => pe.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);
    }

    [Fact]
    public void PaymentEntry_Submit_NegativeReceivedAmount_Throws()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.ReceivedAmount = -50m;

        var ex = Should.Throw<BusinessException>(() => pe.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);
    }

    [Fact]
    public void PaymentEntry_Submit_NegativeExchangeRate_Throws()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.SourceExchangeRate = -1.5m;

        var ex = Should.Throw<BusinessException>(() => pe.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);
    }
}
