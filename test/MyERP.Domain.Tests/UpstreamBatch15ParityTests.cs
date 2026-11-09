using System;
using System.Collections.Generic;
using System.Reflection;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 15:
/// 1) Dunning unallocated payment in cross-currency calculation (PR #64509, commit 6838a4c395; PR #64585, commit d38249cba3).
/// 2) Stock Ledger Entry ProjectId tracking and indexing (PR #64652, commit 6b9f2c01d6).
/// 3) Advance Payment Entry Payment Request tracking & voucher reference (PR #64511, commit 97de257a13).
/// </summary>
public class UpstreamBatch15ParityTests
{
    // === PR #64509 / commit 6838a4c395: Dunning Unallocated Payment in Cross-Currency Calculation ===

    [Fact]
    public void DunningManager_GetPaymentScheduleOutstanding_CalculatesInAccountCurrency_WhenCrossCurrency_6838a4c395()
    {
        // Scenario: Invoice in USD ($1000 total, 2 installments of $500 each at exchange rate 4.5 MYR/USD).
        // Total invoice amount in MYR = 4500 MYR.
        // Payment of $600 USD made against invoice generally (not allocated to specific term rows).
        // Invoice outstanding = $400 USD (1800 MYR).
        // In account currency (MYR):
        // Term 1: 500 * 4.5 = 2250 MYR.
        // Term 2: 500 * 4.5 = 2250 MYR.
        // Unallocated payment in MYR = 4500 - 1800 = 2700 MYR.
        // Term 1 absorbs 2250 MYR -> remaining outstanding = 0 MYR.
        // Term 2 absorbs remaining 450 MYR -> remaining outstanding = 1800 MYR.

        var invoice = new SalesInvoice(
            id: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            invoiceNumber: "SINV-2026-0001",
            issueDate: DateTime.Today)
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
            GrandTotal = 1000m,
            AmountPaid = 600m
        };

        var term1Id = Guid.NewGuid();
        var term2Id = Guid.NewGuid();

        var term1 = new PaymentScheduleEntry(
            id: term1Id,
            parentType: "SalesInvoice",
            parentId: invoice.Id,
            dueDate: DateTime.Today.AddDays(-10),
            invoicePortion: 50m,
            paymentAmount: 500m);

        var term2 = new PaymentScheduleEntry(
            id: term2Id,
            parentType: "SalesInvoice",
            parentId: invoice.Id,
            dueDate: DateTime.Today.AddDays(20),
            invoicePortion: 50m,
            paymentAmount: 500m);

        var schedules = new List<PaymentScheduleEntry> { term1, term2 };

        var results = DunningManager.GetPaymentScheduleOutstanding(
            invoice,
            schedules,
            partyAccountCurrency: "MYR",
            inAccountCurrency: true);

        results.Count.ShouldBe(2);
        results[term1Id].ShouldBe(0m);
        results[term2Id].ShouldBe(1800m);
    }

    [Fact]
    public void DunningManager_GetPaymentScheduleOutstanding_CalculatesInTransactionCurrency_WhenNotCrossCurrency()
    {
        var invoice = new SalesInvoice(
            id: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            invoiceNumber: "SINV-2026-0002",
            issueDate: DateTime.Today)
        {
            CurrencyCode = "MYR",
            ExchangeRate = 1m,
            GrandTotal = 1000m,
            AmountPaid = 600m
        };

        var term1Id = Guid.NewGuid();
        var term2Id = Guid.NewGuid();

        var term1 = new PaymentScheduleEntry(
            id: term1Id,
            parentType: "SalesInvoice",
            parentId: invoice.Id,
            dueDate: DateTime.Today.AddDays(-10),
            invoicePortion: 50m,
            paymentAmount: 500m);

        var term2 = new PaymentScheduleEntry(
            id: term2Id,
            parentType: "SalesInvoice",
            parentId: invoice.Id,
            dueDate: DateTime.Today.AddDays(20),
            invoicePortion: 50m,
            paymentAmount: 500m);

        var schedules = new List<PaymentScheduleEntry> { term1, term2 };

        var results = DunningManager.GetPaymentScheduleOutstanding(
            invoice,
            schedules,
            partyAccountCurrency: "MYR",
            inAccountCurrency: false);

        results.Count.ShouldBe(2);
        results[term1Id].ShouldBe(0m);
        results[term2Id].ShouldBe(400m);
    }

    // === PR #64652 / commit 6b9f2c01d6: Stock Ledger Entry & Stock Entry Item Project Search Index ===

    [Fact]
    public void StockLedgerEntry_SupportsProjectId_ForProjectCosting_6b9f2c01d6()
    {
        var projectId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var sle = new StockLedgerEntry(
            id: Guid.NewGuid(),
            companyId: companyId,
            itemId: itemId,
            warehouseId: warehouseId,
            postingDate: DateTime.UtcNow,
            quantityChange: 10m,
            valuationRate: 15.5m,
            balanceQuantity: 10m,
            balanceValue: 155m)
        {
            ProjectId = projectId
        };

        sle.ProjectId.ShouldBe(projectId);
    }

    [Fact]
    public void StockEntryItem_SupportsProjectId_ForProjectTracking_6b9f2c01d6()
    {
        var projectId = Guid.NewGuid();
        var item = new StockEntryItem(
            id: Guid.NewGuid(),
            stockEntryId: Guid.NewGuid(),
            itemId: Guid.NewGuid(),
            quantity: 5m,
            sourceWarehouseId: Guid.NewGuid(),
            targetWarehouseId: null)
        {
            ProjectId = projectId
        };

        item.ProjectId.ShouldBe(projectId);
    }

    // === PR #64511 / commit 97de257a13: Advance Payment Entry Payment Request Tracking & Voucher Reference ===

    [Fact]
    public void PaymentEntryReference_TracksPaymentRequestAndAdvanceVoucher_97de257a13()
    {
        var peId = Guid.NewGuid();
        var refId = Guid.NewGuid();
        var paymentRequestId = Guid.NewGuid();
        var advanceVoucherId = Guid.NewGuid();

        var reference = new PaymentEntryReference(
            id: Guid.NewGuid(),
            paymentEntryId: peId,
            referenceType: "SalesInvoice",
            referenceId: refId,
            totalAmount: 1200m,
            outstandingAmount: 600m,
            allocatedAmount: 600m,
            referenceNumber: "ACC-SINV-2026-0001")
        {
            PaymentRequestId = paymentRequestId,
            AdvanceVoucherType = "SalesOrder",
            AdvanceVoucherId = advanceVoucherId
        };

        reference.PaymentRequestId.ShouldBe(paymentRequestId);
        reference.AdvanceVoucherType.ShouldBe("SalesOrder");
        reference.AdvanceVoucherId.ShouldBe(advanceVoucherId);
    }

    [Fact]
    public void PaymentReferenceDto_ExposesPaymentRequestAndAdvanceVoucherFields_97de257a13()
    {
        var dtoProps = typeof(PaymentReferenceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in dtoProps)
        {
            propNames.Add(p.Name);
        }

        propNames.ShouldContain("PaymentRequestId");
        propNames.ShouldContain("AdvanceVoucherType");
        propNames.ShouldContain("AdvanceVoucherId");
        propNames.ShouldContain("TotalAmount");
        propNames.ShouldContain("OutstandingAmount");
        propNames.ShouldContain("AllocatedAmount");
    }
}
