using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class PaymentRequestTests
{
    private static PaymentRequest CreatePR() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            Guid.NewGuid(), "Customer", 5000m);

    [Fact]
    public void Create_SetsDefaults()
    {
        var pr = CreatePR();
        pr.Status.ShouldBe(PaymentRequestStatus.Draft);
        pr.GrandTotal.ShouldBe(5000m);
        pr.OutstandingAmount.ShouldBe(5000m);
    }

    [Fact]
    public void Submit_Succeeds()
    {
        var pr = CreatePR();
        pr.Submit();
        pr.Status.ShouldBe(PaymentRequestStatus.Initiated);
    }

    [Fact]
    public void MarkPaid_SetsPaymentEntry()
    {
        var pr = CreatePR();
        pr.Submit();
        var peId = Guid.NewGuid();
        pr.MarkPaid(peId);
        pr.Status.ShouldBe(PaymentRequestStatus.Paid);
        pr.PaymentEntryId.ShouldBe(peId);
    }

    [Fact]
    public void Cancel_Paid_Throws()
    {
        var pr = CreatePR();
        pr.Submit();
        pr.MarkPaid(Guid.NewGuid());
        Should.Throw<BusinessException>(() => pr.Cancel());
    }

    [Fact]
    public void Cancel_Initiated_Succeeds()
    {
        var pr = CreatePR();
        pr.Submit();
        pr.Cancel();
        pr.Status.ShouldBe(PaymentRequestStatus.Cancelled);
    }
}
