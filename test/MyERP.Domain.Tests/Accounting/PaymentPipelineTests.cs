using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Tests for Payment Entry business rules and the full payment pipeline:
/// - Multi-reference allocation correctness
/// - Exchange gain/loss calculation
/// - Payment schedule FIFO allocation
/// - Term-based allocation validation
/// - Stale outstanding detection
/// </summary>
public class PaymentPipelineTests
{
    // ========== PaymentEntry Multi-Reference Allocation ==========

    private static PaymentEntry CreatePE(decimal amount, PaymentType type = PaymentType.Receive)
    {
        return new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), type, DateTime.UtcNow,
            amount, Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact]
    public void PE_MultiRef_TotalAllocated_SumsReferences()
    {
        var pe = CreatePE(10000);
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(),
            6000, 6000, 6000));
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(),
            4000, 4000, 4000));

        var totalAllocated = pe.References.Sum(r => r.AllocatedAmount);
        totalAllocated.ShouldBe(10000);
        pe.UnallocatedAmount.ShouldBe(0);
    }

    [Fact]
    public void PE_MultiRef_PartialAllocation_ShowsUnallocated()
    {
        var pe = CreatePE(10000);
        pe.References.Add(new PaymentEntryReference(
            Guid.NewGuid(), pe.Id, "SalesInvoice", Guid.NewGuid(),
            7000, 7000, 7000));

        pe.UnallocatedAmount.ShouldBe(3000);
    }

    [Fact]
    public void PE_NoReferences_NoInvoice_FullyUnallocated()
    {
        var pe = CreatePE(5000);
        pe.UnallocatedAmount.ShouldBe(5000);
    }

    // ========== Exchange Gain/Loss ==========

    [Fact]
    public void PE_ExchangeGainLoss_HigherPaymentRate_IsGain()
    {
        var pe = CreatePE(1000);
        pe.ExchangeRate = 4.80m;       // Payment at 4.80
        pe.SourceExchangeRate = 4.50m; // Invoice was at 4.50

        // Gain = 1000 × (4.80 - 4.50) = 300
        pe.ExchangeGainLoss.ShouldBe(300);
    }

    [Fact]
    public void PE_ExchangeGainLoss_LowerPaymentRate_IsLoss()
    {
        var pe = CreatePE(1000);
        pe.ExchangeRate = 4.30m;       // Payment at 4.30
        pe.SourceExchangeRate = 4.50m; // Invoice was at 4.50

        // Loss = 1000 × (4.30 - 4.50) = -200
        pe.ExchangeGainLoss.ShouldBe(-200);
    }

    [Fact]
    public void PE_ExchangeGainLoss_SameRate_IsZero()
    {
        var pe = CreatePE(5000);
        pe.ExchangeRate = 1m;
        pe.SourceExchangeRate = 1m;
        pe.ExchangeGainLoss.ShouldBe(0);
    }

    [Fact]
    public void PE_BaseAmount_IsAmountTimesExchangeRate()
    {
        var pe = CreatePE(1000);  // USD 1000
        pe.ExchangeRate = 4.72m; // 1 USD = 4.72 MYR

        pe.BaseAmount.ShouldBe(4720m); // MYR equivalent
    }

    // ========== Payment Schedule FIFO Allocation ==========

    [Fact]
    public void PaymentSchedule_RecordPayment_ReducesOutstanding()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100m, 1000m);

        var allocated = entry.RecordPayment(300);

        allocated.ShouldBe(300);
        entry.PaidAmount.ShouldBe(300);
        entry.Outstanding.ShouldBe(700);
    }

    [Fact]
    public void PaymentSchedule_RecordPayment_CapsAtOutstanding()
    {
        var entry = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100m, 500m);
        entry.RecordPayment(400); // Pay 400 first

        var allocated = entry.RecordPayment(200); // Try to pay 200 but only 100 remaining

        allocated.ShouldBe(100); // Capped at outstanding
        entry.PaidAmount.ShouldBe(500);
        entry.Outstanding.ShouldBe(0);
        entry.IsFullyPaid.ShouldBeTrue();
    }

    [Fact]
    public void PaymentSchedule_MultiTerm_FIFO_EarliestFirst()
    {
        // 3-term schedule: 40% (due day 30), 30% (day 60), 30% (day 90)
        var parentId = Guid.NewGuid();
        var terms = new[]
        {
            new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", parentId, DateTime.UtcNow.AddDays(30), 40m, 400m),
            new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", parentId, DateTime.UtcNow.AddDays(60), 30m, 300m),
            new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", parentId, DateTime.UtcNow.AddDays(90), 30m, 300m),
        };

        // Pay 500: should fill first term (400) then partial second (100)
        var remaining = 500m;
        foreach (var term in terms.OrderBy(t => t.DueDate))
        {
            if (remaining <= 0) break;
            var allocated = term.RecordPayment(remaining);
            remaining -= allocated;
        }

        terms[0].PaidAmount.ShouldBe(400); // Fully paid
        terms[0].IsFullyPaid.ShouldBeTrue();
        terms[1].PaidAmount.ShouldBe(100); // Partial
        terms[1].IsFullyPaid.ShouldBeFalse();
        terms[2].PaidAmount.ShouldBe(0);   // Untouched
        remaining.ShouldBe(0);
    }

    // ========== Stale Outstanding Guard ==========

    [Fact]
    public void StaleOutstandingGuard_WithinLimit_Passes()
    {
        var pe = CreatePE(5000);
        var mgr = new PaymentEntryManager(null!);

        // Outstanding is 5000, paying 5000 — within limit
        mgr.ValidateAllocationNotExceedsOutstanding(pe, 5000);
        // Should not throw
    }

    [Fact]
    public void StaleOutstandingGuard_ExceedsOutstanding_Throws()
    {
        var pe = CreatePE(5000);
        var mgr = new PaymentEntryManager(null!);

        // Outstanding is only 3000 (someone else paid 2000 concurrently)
        var ex = Should.Throw<BusinessException>(
            () => mgr.ValidateAllocationNotExceedsOutstanding(pe, 3000));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.OverAllocation);
    }

    [Fact]
    public void StaleOutstandingGuard_ZeroOutstanding_Passes()
    {
        var pe = CreatePE(5000);
        var mgr = new PaymentEntryManager(null!);

        // Per ERPNext: outstanding <= 0 is a soft warning, NOT hard error
        mgr.ValidateAllocationNotExceedsOutstanding(pe, 0);
        // Should not throw (PE against already-paid invoice is warning-only)
    }

    // ========== Invoice Outstanding Impact ==========

    [Fact]
    public void SalesInvoice_PaymentReducesOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m);

        si.GrandTotal.ShouldBe(1000);
        si.OutstandingAmount.ShouldBe(1000);

        si.AmountPaid = 400;
        si.OutstandingAmount.ShouldBe(600);

        si.AmountPaid = 1000;
        si.OutstandingAmount.ShouldBe(0);
    }

    [Fact]
    public void PurchaseInvoice_PaymentReducesOutstanding()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Steel", 5m, 200m, 0m);

        pi.GrandTotal.ShouldBe(1000);
        pi.OutstandingAmount.ShouldBe(1000);

        pi.AmountPaid = 600;
        pi.OutstandingAmount.ShouldBe(400);
    }

    // ========== Advance Payment (IsAdvance computed property) ==========

    [Fact]
    public void PE_IsAdvance_WhenOrderLinkedButNoInvoice()
    {
        var pe = CreatePE(5000);
        pe.AgainstOrderId = Guid.NewGuid();
        pe.AgainstOrderType = "SalesOrder";

        pe.IsAdvance.ShouldBeTrue();
    }

    [Fact]
    public void PE_IsNotAdvance_WhenInvoiceLinked()
    {
        var pe = CreatePE(5000);
        pe.AgainstInvoiceId = Guid.NewGuid();
        pe.AgainstOrderId = Guid.NewGuid(); // Both set

        pe.IsAdvance.ShouldBeFalse(); // Invoice takes priority
    }

    [Fact]
    public void PE_IsNotAdvance_WhenNothingLinked()
    {
        var pe = CreatePE(5000);
        pe.IsAdvance.ShouldBeFalse();
    }
}
