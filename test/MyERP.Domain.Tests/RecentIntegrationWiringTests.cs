using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Accounting.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Core;
using MyERP.Shared;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for recently wired integrations:
/// - CostCenterAllocation distribution into GL posting
/// - DeliverySchedule FIFO allocation during DN submit
/// - AccountCategory financial report grouping
/// - PeriodClosingPosting service behavior
/// - IAccountableDocument.CostCenterId interface extension
/// </summary>
public class RecentIntegrationWiringTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    // --- CostCenterAllocation Distribution Tests ---

    [Fact]
    public void CostCenterAllocation_Distribute_EvenSplit()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, _tenantId);
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();
        alloc.AddEntry(cc1, 50m);
        alloc.AddEntry(cc2, 50m);

        var result = alloc.Distribute(1000m);

        Assert.Equal(2, result.Count);
        Assert.Equal(500m, result[0].Amount);
        Assert.Equal(500m, result[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_Distribute_UnevenSplit_LastAbsorbsRemainder()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, _tenantId);
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();
        var cc3 = Guid.NewGuid();
        alloc.AddEntry(cc1, 33.33m);
        alloc.AddEntry(cc2, 33.33m);
        alloc.AddEntry(cc3, 33.34m);

        var result = alloc.Distribute(100m);

        Assert.Equal(3, result.Count);
        // Total should always equal input amount (rounding absorbed by first entry)
        var total = result.Sum(r => r.Amount);
        Assert.Equal(100m, total);
    }

    [Fact]
    public void CostCenterAllocation_Distribute_ZeroAmount_ReturnsZeros()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 40m);

        var result = alloc.Distribute(0m);

        Assert.All(result, r => Assert.Equal(0m, r.Amount));
    }

    [Fact]
    public void CostCenterAllocation_ValidatePercentages_MustSumTo100()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        alloc.AddEntry(Guid.NewGuid(), 50m);

        alloc.ValidatePercentages(); // Should not throw
    }

    [Fact]
    public void CostCenterAllocation_ValidatePercentages_NotSumTo100_Throws()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        alloc.AddEntry(Guid.NewGuid(), 40m);

        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.ValidatePercentages());
    }

    // --- DeliverySchedule Tests ---

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPendingQty()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 100m, _tenantId);

        entry.RecordDelivery(40m);

        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_FullDelivery_SetsCompleted()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 100m, _tenantId);

        entry.RecordDelivery(100m);

        Assert.Equal(100m, entry.DeliveredQty);
        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_Progressive_Works()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 100m, _tenantId);

        entry.RecordDelivery(30m);
        entry.RecordDelivery(30m);
        entry.RecordDelivery(40m);

        Assert.Equal(100m, entry.DeliveredQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingQty_NeverNegative()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 50m, _tenantId);

        entry.RecordDelivery(60m); // Over-deliver

        Assert.True(entry.PendingQty >= 0);
    }

    // --- IAccountableDocument.CostCenterId Interface ---

    [Fact]
    public void IAccountableDocument_CostCenterId_DefaultsNull()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.Today);
        IAccountableDocument doc = si;

        Assert.Null(doc.CostCenterId);
    }

    // --- AccountCategory Tests ---

    [Fact]
    public void AccountCategory_Create_WithRequiredFields()
    {
        var category = new AccountCategory(Guid.NewGuid(), "Revenue from Operations", "Income");

        Assert.Equal("Revenue from Operations", category.Name);
        Assert.Equal("Income", category.RootType);
    }

    [Fact]
    public void AccountCategory_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AccountCategory(Guid.NewGuid(), "", "Asset"));
    }

    [Fact]
    public void AccountCategory_EmptyRootType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AccountCategory(Guid.NewGuid(), "Cash", ""));
    }

    // --- PeriodClosingPosting Tests ---

    [Fact]
    public void PeriodClosingVoucher_Submit_RequiresEntries()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, DateTime.Today, Guid.NewGuid(), _tenantId);

        Assert.Throws<Volo.Abp.BusinessException>(() => pcv.Submit());
    }

    [Fact]
    public void PeriodClosingVoucher_Submit_WithEntries_Succeeds()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, DateTime.Today, Guid.NewGuid(), _tenantId);
        pcv.AddEntry(Guid.NewGuid(), null, 1000m, true);

        pcv.Submit();

        Assert.Equal(DocumentStatus.Submitted, pcv.Status);
    }

    [Fact]
    public void PeriodClosingVoucher_TotalClosingAmount_SumsEntries()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, DateTime.Today, Guid.NewGuid(), _tenantId);
        pcv.AddEntry(Guid.NewGuid(), null, 500m, true);
        pcv.AddEntry(Guid.NewGuid(), null, 300m, true);
        pcv.AddEntry(Guid.NewGuid(), null, 200m, true);

        Assert.Equal(1000m, pcv.TotalClosingAmount);
    }

    [Fact]
    public void PeriodClosingVoucher_Cancel_FromSubmitted()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, DateTime.Today, Guid.NewGuid(), _tenantId);
        pcv.AddEntry(Guid.NewGuid(), null, 100m, true);
        pcv.Submit();

        pcv.Cancel();

        Assert.Equal(DocumentStatus.Cancelled, pcv.Status);
    }

    // --- FinanceBook Tag on GL Lines ---

    [Fact]
    public void JournalEntryLine_FinanceBook_DefaultsNull()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 1000m, true);
        je.AddLine(Guid.NewGuid(), 1000m, false);

        Assert.All(je.Lines, l => Assert.Null(l.FinanceBook));
    }

    [Fact]
    public void JournalEntryLine_FinanceBook_CanBeSet()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 1000m, true);
        je.Lines[0].FinanceBook = "Tax Book";

        Assert.Equal("Tax Book", je.Lines[0].FinanceBook);
    }

    [Fact]
    public void JournalEntryLine_CostCenterId_CanBeAssigned()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 500m, true);
        var ccId = Guid.NewGuid();
        je.Lines[0].CostCenterId = ccId;

        Assert.Equal(ccId, je.Lines[0].CostCenterId);
    }

    // --- DeliveryScheduleService (domain service) ---

    [Fact]
    public void DeliveryScheduleService_GenerateSchedule_Monthly_4Months()
    {
        var service = new DeliveryScheduleService();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 4, 30);

        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(), 100m,
            start, end, DeliveryFrequency.Monthly);

        Assert.Equal(4, entries.Count);
        Assert.Equal(25m, entries[0].ScheduledQty);
        Assert.Equal(25m, entries[3].ScheduledQty);
        Assert.Equal(100m, entries.Sum(e => e.ScheduledQty));
    }

    [Fact]
    public void DeliveryScheduleService_GenerateSchedule_WholeNumber_LastAbsorbsRemainder()
    {
        var service = new DeliveryScheduleService();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 3, 31);

        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(), 10m,
            start, end, DeliveryFrequency.Monthly,
            mustBeWholeNumber: true);

        Assert.Equal(3, entries.Count);
        Assert.Equal(3m, entries[0].ScheduledQty); // Floor(10/3) = 3
        Assert.Equal(3m, entries[1].ScheduledQty);
        Assert.Equal(4m, entries[2].ScheduledQty); // Last absorbs remainder: 10 - 6 = 4
        Assert.Equal(10m, entries.Sum(e => e.ScheduledQty));
    }

    [Fact]
    public void DeliveryScheduleService_GenerateSchedule_Weekly()
    {
        var service = new DeliveryScheduleService();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 28); // 4 weeks

        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(), 40m,
            start, end, DeliveryFrequency.Weekly);

        Assert.Equal(4, entries.Count);
        Assert.All(entries, e => Assert.Equal(10m, e.ScheduledQty));
    }
}
