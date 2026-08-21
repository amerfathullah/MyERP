using System;
using System.Collections.Generic;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class AutoAllocateTests
{
    private static UnreconciledPayment Payment(decimal amount, string type = "PaymentEntry") => new()
    {
        VoucherType = type,
        VoucherId = Guid.NewGuid(),
        TotalAmount = amount,
        UnallocatedAmount = amount,
    };

    private static OutstandingVoucher Invoice(decimal outstanding, string type = "SalesInvoice") => new()
    {
        VoucherType = type,
        VoucherId = Guid.NewGuid(),
        Outstanding = outstanding,
    };

    [Fact]
    public void AutoAllocate_PaymentExactlyCoversInvoice_OneAllocation()
    {
        var payment = Payment(500m);
        var invoice = Invoice(500m);

        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment> { payment },
            new List<OutstandingVoucher> { invoice });

        result.Count.ShouldBe(1);
        result[0].AllocatedAmount.ShouldBe(500m);
        result[0].PaymentVoucherId.ShouldBe(payment.VoucherId);
        result[0].InvoiceVoucherId.ShouldBe(invoice.VoucherId);
    }

    [Fact]
    public void AutoAllocate_PaymentSmallerThanInvoice_PartialAllocation()
    {
        var payment = Payment(300m);
        var invoice = Invoice(500m);

        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment> { payment },
            new List<OutstandingVoucher> { invoice });

        result.Count.ShouldBe(1);
        result[0].AllocatedAmount.ShouldBe(300m);
    }

    [Fact]
    public void AutoAllocate_PaymentLargerThanInvoice_SpillsToNextInvoice()
    {
        var payment = Payment(800m);
        var invoiceA = Invoice(500m);
        var invoiceB = Invoice(300m);

        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment> { payment },
            new List<OutstandingVoucher> { invoiceA, invoiceB });

        result.Count.ShouldBe(2);
        result[0].InvoiceVoucherId.ShouldBe(invoiceA.VoucherId);
        result[0].AllocatedAmount.ShouldBe(500m);
        result[1].InvoiceVoucherId.ShouldBe(invoiceB.VoucherId);
        result[1].AllocatedAmount.ShouldBe(300m);
    }

    [Fact]
    public void AutoAllocate_MultiplePayments_EachConsumesRemainingInvoiceCapacity()
    {
        var paymentA = Payment(400m);
        var paymentB = Payment(400m);
        var invoice = Invoice(500m);

        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment> { paymentA, paymentB },
            new List<OutstandingVoucher> { invoice });

        result.Count.ShouldBe(2);
        result[0].AllocatedAmount.ShouldBe(400m); // paymentA takes 400 of 500
        result[1].AllocatedAmount.ShouldBe(100m); // paymentB only gets the remaining 100
    }

    [Fact]
    public void AutoAllocate_NoInvoices_ReturnsEmpty()
    {
        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment> { Payment(500m) },
            new List<OutstandingVoucher>());

        result.ShouldBeEmpty();
    }

    [Fact]
    public void AutoAllocate_NoPayments_ReturnsEmpty()
    {
        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment>(),
            new List<OutstandingVoucher> { Invoice(500m) });

        result.ShouldBeEmpty();
    }

    [Fact]
    public void AutoAllocate_ZeroOutstandingInvoice_Skipped()
    {
        var payment = Payment(500m);
        var zeroInvoice = Invoice(0m);
        var realInvoice = Invoice(200m);

        var result = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment> { payment },
            new List<OutstandingVoucher> { zeroInvoice, realInvoice });

        result.Count.ShouldBe(1);
        result[0].InvoiceVoucherId.ShouldBe(realInvoice.VoucherId);
        result[0].AllocatedAmount.ShouldBe(200m);
    }
}

public class ProcessPaymentReconciliationTests
{
    private static ProcessPaymentReconciliation NewRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Customer", Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Submit_FromDraft_MovesToQueued()
    {
        var request = NewRequest();
        request.Submit();
        request.Status.ShouldBe(ProcessPaymentReconciliationStatus.Queued);
    }

    [Fact]
    public void Submit_NotFromDraft_Throws()
    {
        var request = NewRequest();
        request.Submit();
        Should.Throw<BusinessException>(() => request.Submit());
    }

    [Fact]
    public void StartProcessing_FromQueued_MovesToRunning()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        request.Status.ShouldBe(ProcessPaymentReconciliationStatus.Running);
    }

    [Fact]
    public void StartProcessing_Retriable_FromRunningAgain()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        Should.NotThrow(() => request.StartProcessing());
        request.Status.ShouldBe(ProcessPaymentReconciliationStatus.Running);
    }

    [Fact]
    public void StartProcessing_AfterCompleted_Throws()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        request.Complete();
        Should.Throw<BusinessException>(() => request.StartProcessing());
    }

    [Fact]
    public void RecordFailure_WithNoProgress_MarksFailed()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        request.RecordFailure("boom");
        request.Status.ShouldBe(ProcessPaymentReconciliationStatus.Failed);
        request.ErrorLog.ShouldBe("boom");
    }

    [Fact]
    public void RecordFailure_AfterSomeProgress_MarksPartiallyReconciled()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        request.RecordProgress(1);
        request.RecordFailure("boom");
        request.Status.ShouldBe(ProcessPaymentReconciliationStatus.PartiallyReconciled);
    }

    [Fact]
    public void Cancel_FromRunning_Succeeds()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        request.Cancel();
        request.Status.ShouldBe(ProcessPaymentReconciliationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AfterCompleted_Throws()
    {
        var request = NewRequest();
        request.Submit();
        request.StartProcessing();
        request.Complete();
        Should.Throw<BusinessException>(() => request.Cancel());
    }
}
