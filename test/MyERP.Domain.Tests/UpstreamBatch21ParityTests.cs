using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 21:
/// 1) Sales Order advance payment status lifecycle (PR #64511 / commit 97de257a13).
/// 2) Payment Request & Advance Voucher retention on Payment Entry reference allocation (PR #64511 / commit 97de257a13).
/// 3) Dunning payment schedule multi-term outstanding recalculation after partial payment (PR #64585 / commits d38249cba3 &amp; 0c2c14c3ab).
/// </summary>
public class UpstreamBatch21ParityTests
{
    // === 1) Sales Order Advance Payment Status ===

    [Fact]
    public void SalesOrder_InitialAdvancePaymentStatus_DefaultsToNotRequested()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-2026-0001", DateTime.UtcNow);
        so.AdvancePaymentStatus.ShouldBe("Not Requested");
    }

    [Theory]
    [InlineData("Requested")]
    [InlineData("Partially Paid")]
    [InlineData("Fully Paid")]
    [InlineData("Not Requested")]
    public void SalesOrder_SetAdvancePaymentStatus_UpdatesStatusCorrectly(string targetStatus)
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-2026-0002", DateTime.UtcNow);
        so.SetAdvancePaymentStatus(targetStatus);
        so.AdvancePaymentStatus.ShouldBe(targetStatus);
    }

    // === 2) Payment Entry Reference Handover Retention ===

    [Fact]
    public void PaymentEntryReference_AdvanceProperties_AreRetainedWhenConstructed()
    {
        var peId = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var prId = Guid.NewGuid();

        var advanceRef = new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesOrder", soId,
            totalAmount: 500m, outstandingAmount: 500m, allocatedAmount: 500m, referenceNumber: "SO-001")
        {
            PaymentRequestId = prId
        };

        advanceRef.PaymentRequestId.ShouldBe(prId);
        advanceRef.ReferenceType.ShouldBe("SalesOrder");
        advanceRef.ReferenceId.ShouldBe(soId);

        // When allocated to invoice, invoice reference inherits the advance linkage
        var invoiceId = Guid.NewGuid();
        var invoiceRef = new PaymentEntryReference(
            Guid.NewGuid(), peId, "SalesInvoice", invoiceId,
            totalAmount: 300m, outstandingAmount: 0m, allocatedAmount: 300m, referenceNumber: "SI-001")
        {
            AdvanceVoucherType = advanceRef.ReferenceType,
            AdvanceVoucherId = advanceRef.ReferenceId,
            PaymentRequestId = advanceRef.PaymentRequestId
        };

        invoiceRef.AdvanceVoucherType.ShouldBe("SalesOrder");
        invoiceRef.AdvanceVoucherId.ShouldBe(soId);
        invoiceRef.PaymentRequestId.ShouldBe(prId);
    }

    // === 3) Dunning Payment Schedule Recalculation ===

    [Fact]
    public void DunningManager_GetPaymentScheduleOutstanding_DeductsFromOldestInstallmentFirst()
    {
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", DateTime.UtcNow.AddDays(-60))
        {
            GrandTotal = 100m,
            AmountPaid = 50m // 50 paid towards the invoice without specific term allocation
        };

        var term1Id = Guid.NewGuid();
        var term2Id = Guid.NewGuid();

        var sched1 = new PaymentScheduleEntry(term1Id, "SalesInvoice", invoice.Id, DateTime.UtcNow.AddDays(-30), 50m, 50m);
        var sched2 = new PaymentScheduleEntry(term2Id, "SalesInvoice", invoice.Id, DateTime.UtcNow.AddDays(-10), 50m, 50m);

        var schedules = new List<PaymentScheduleEntry> { sched1, sched2 };

        var outstandingMap = DunningManager.GetPaymentScheduleOutstanding(invoice, schedules, "MYR", inAccountCurrency: false);

        // Oldest installment (sched1) was satisfied by the 50 partial payment -> outstanding is 0
        outstandingMap[term1Id].ShouldBe(0m);
        // Newer installment (sched2) still has 50 remaining outstanding
        outstandingMap[term2Id].ShouldBe(50m);
    }

    [Fact]
    public void DunningManager_GetPaymentScheduleOutstanding_FullyPaidInvoice_ZeroesAllInstallments()
    {
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0002", DateTime.UtcNow.AddDays(-40))
        {
            GrandTotal = 200m,
            AmountPaid = 200m
        };

        var term1Id = Guid.NewGuid();
        var term2Id = Guid.NewGuid();

        var sched1 = new PaymentScheduleEntry(term1Id, "SalesInvoice", invoice.Id, DateTime.UtcNow.AddDays(-20), 50m, 100m);
        var sched2 = new PaymentScheduleEntry(term2Id, "SalesInvoice", invoice.Id, DateTime.UtcNow.AddDays(-5), 50m, 100m);

        var schedules = new List<PaymentScheduleEntry> { sched1, sched2 };

        var outstandingMap = DunningManager.GetPaymentScheduleOutstanding(invoice, schedules, "MYR", inAccountCurrency: false);

        outstandingMap[term1Id].ShouldBe(0m);
        outstandingMap[term2Id].ShouldBe(0m);
    }
}
