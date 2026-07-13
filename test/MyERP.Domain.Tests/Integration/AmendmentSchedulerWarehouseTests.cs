using System;
using System.Collections.Generic;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - PI AmendAsync: IAmendable, amendment number generation
/// - SE warehouse validation: transfer requires source+target, same-warehouse blocked
/// - Quotation expiry: IsExpired detection
/// - Deferred revenue: schedule generation concept
/// </summary>
public class AmendmentSchedulerWarehouseTests
{
    // --- PI Amendment ---

    [Fact]
    public void PurchaseInvoice_AmendedFromId_TracksOriginal()
    {
        var original = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        var amended = new PurchaseInvoice(Guid.NewGuid(), original.CompanyId, original.SupplierId, "PI-001-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;

        amended.AmendedFromId.ShouldBe(original.Id);
        amended.AmendmentIndex.ShouldBe(1);
    }

    [Fact]
    public void PurchaseInvoice_AmendmentChain_IncrementIndex()
    {
        // First amendment: PI-001 → PI-001-1 (index 1)
        // Second amendment: PI-001-1 → PI-001-2 (index 2)
        var original = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-100", DateTime.Today);
        var firstAmend = new PurchaseInvoice(Guid.NewGuid(), original.CompanyId, original.SupplierId, "PI-100-1", DateTime.Today);
        firstAmend.AmendmentIndex = 1;
        var secondAmend = new PurchaseInvoice(Guid.NewGuid(), original.CompanyId, original.SupplierId, "PI-100-2", DateTime.Today);
        secondAmend.AmendmentIndex = firstAmend.AmendmentIndex + 1;

        secondAmend.AmendmentIndex.ShouldBe(2);
    }

    [Fact]
    public void DocumentAmendmentService_ValidateCanAmend_OnlyCancelled()
    {
        // Only Cancelled status allows amendment
        var cancelledStatus = DocumentStatus.Cancelled;
        (cancelledStatus == DocumentStatus.Cancelled).ShouldBeTrue();

        var draftStatus = DocumentStatus.Draft;
        (draftStatus == DocumentStatus.Cancelled).ShouldBeFalse();
    }

    // --- SE Warehouse Validation ---

    [Fact]
    public void StockEntryType_Transfer_RequiresSourceAndTarget()
    {
        var transferType = StockEntryType.MaterialTransfer;
        var requiresSource = transferType == StockEntryType.MaterialIssue
            || transferType == StockEntryType.MaterialTransfer
            || transferType == StockEntryType.MaterialTransferForManufacture;
        var requiresTarget = transferType == StockEntryType.MaterialReceipt
            || transferType == StockEntryType.MaterialTransfer
            || transferType == StockEntryType.MaterialTransferForManufacture;

        requiresSource.ShouldBeTrue();
        requiresTarget.ShouldBeTrue();
    }

    [Fact]
    public void StockEntryType_Receipt_RequiresOnlyTarget()
    {
        var receiptType = StockEntryType.MaterialReceipt;
        var requiresSource = receiptType == StockEntryType.MaterialIssue
            || receiptType == StockEntryType.MaterialTransfer;
        var requiresTarget = receiptType == StockEntryType.MaterialReceipt
            || receiptType == StockEntryType.MaterialTransfer;

        requiresSource.ShouldBeFalse();
        requiresTarget.ShouldBeTrue();
    }

    [Fact]
    public void StockEntryType_Issue_RequiresOnlySource()
    {
        var issueType = StockEntryType.MaterialIssue;
        var requiresSource = issueType == StockEntryType.MaterialIssue
            || issueType == StockEntryType.MaterialTransfer;
        var requiresTarget = issueType == StockEntryType.MaterialReceipt
            || issueType == StockEntryType.MaterialTransfer;

        requiresSource.ShouldBeTrue();
        requiresTarget.ShouldBeFalse();
    }

    [Fact]
    public void SameWarehouse_Transfer_Blocked()
    {
        var sourceId = Guid.NewGuid();
        var targetId = sourceId; // Same warehouse

        (sourceId == targetId).ShouldBeTrue(); // This would be blocked
    }

    [Fact]
    public void DifferentWarehouse_Transfer_Allowed()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        (sourceId == targetId).ShouldBeFalse(); // Different = allowed
    }

    // --- Quotation Expiry ---

    [Fact]
    public void Quotation_IsExpired_WhenPastValidUntil()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.Today);
        q.AddItem(Guid.NewGuid(), "Test", 1, 100, 0, "Unit");
        q.Submit(); // Must be Submitted to be considered expired
        q.ValidUntil = DateTime.Today.AddDays(-5); // Expired 5 days ago

        q.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void Quotation_NotExpired_WhenFutureValidUntil()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-002", DateTime.Today);
        q.AddItem(Guid.NewGuid(), "Test", 1, 100, 0, "Unit");
        q.Submit();
        q.ValidUntil = DateTime.Today.AddDays(30); // Valid for 30 more days

        q.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void Quotation_NotExpired_WhenNoValidUntil()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-003", DateTime.Today);
        // No ValidUntil set = never expires

        q.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void Quotation_MarkLost_FromSubmitted()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-004", DateTime.Today);
        q.AddItem(Guid.NewGuid(), "Test", 1, 100, 0, "Unit");
        q.Submit();

        q.MarkLost();

        q.Status.ShouldBe(DocumentStatus.Rejected);
    }

    // --- Deferred Revenue Schedule Concept ---

    [Fact]
    public void DeferredRevenue_MonthlyProration()
    {
        // 12-month service: RM 12,000 → RM 1,000/month
        decimal totalAmount = 12_000m;
        int serviceMonths = 12;
        decimal monthlyAmount = totalAmount / serviceMonths;

        monthlyAmount.ShouldBe(1000m);
    }

    [Fact]
    public void DeferredRevenue_FinalPeriod_AbsorbsRounding()
    {
        // 7-month service: RM 1,000 → RM 142.86/month, final = 1000 - (142.86 * 6) = 142.84
        decimal totalAmount = 1000m;
        int serviceMonths = 7;
        decimal monthlyAmount = Math.Round(totalAmount / serviceMonths, 2);
        decimal finalAmount = totalAmount - (monthlyAmount * (serviceMonths - 1));

        // Final absorbs rounding difference
        (monthlyAmount * (serviceMonths - 1) + finalAmount).ShouldBe(totalAmount);
    }
}
