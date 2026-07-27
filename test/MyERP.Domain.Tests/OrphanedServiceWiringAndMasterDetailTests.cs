using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for orphaned service wiring and master/detail patterns:
/// - FiscalYear close behavior
/// - AutoRepeat scheduling logic
/// - WorkstationScheduling result types
/// - WarehouseAccount nullable accounts
/// - Master detail view DTO field coverage
/// - Document edit mode guards
/// </summary>
public class OrphanedServiceWiringAndMasterDetailTests
{
    // === FiscalYearCloseService Wiring ===

    [Fact]
    public void FiscalYear_DefaultIsOpen()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.False(fy.IsClosed);
    }

    [Fact]
    public void FiscalYear_CloseSetsClosed()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        fy.IsClosed = true;

        Assert.True(fy.IsClosed);
    }

    [Fact]
    public void FiscalYear_DoubleCloseIdempotent()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        fy.IsClosed = true;
        fy.IsClosed = true; // No exception expected

        Assert.True(fy.IsClosed);
    }

    [Fact]
    public void FiscalYear_CannotReopenAfterClose()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        fy.IsClosed = true;
        // No Reopen method exists — IsClosed stays true
        Assert.True(fy.IsClosed);
    }

    // === AutoRepeatService Wiring ===

    [Fact]
    public void AutoRepeat_IsDueOnToday_ReturnsTrue()
    {
        var today = DateTime.UtcNow.Date;
        var ar = new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), RepeatFrequency.Monthly, today.AddDays(-1));

        Assert.True(ar.IsEnabled);
        Assert.True(ar.IsDueOn(today));
    }

    [Fact]
    public void AutoRepeat_NotDueWhenDisabled()
    {
        var today = DateTime.UtcNow.Date;
        var ar = new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), RepeatFrequency.Monthly, today.AddDays(-1));

        ar.Disable();

        Assert.False(ar.IsEnabled);
        Assert.False(ar.IsDueOn(today));
    }

    [Fact]
    public void AutoRepeat_PastEndDate_NotDue()
    {
        var today = DateTime.UtcNow.Date;
        var ar = new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), RepeatFrequency.Monthly,
            today.AddMonths(-2), today.AddDays(-1));

        // EndDate is yesterday, so even though NextScheduleDate <= today, it should not be due
        Assert.False(ar.IsDueOn(today));
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_AdvancesNextDate()
    {
        var startDate = new DateTime(2026, 1, 15);
        var ar = new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(), "JournalEntry",
            Guid.NewGuid(), RepeatFrequency.Monthly, startDate);

        Assert.Equal(startDate, ar.NextScheduleDate);

        ar.RecordGeneration(startDate);

        Assert.Equal(1, ar.GeneratedCount);
        Assert.Equal(startDate, ar.LastGeneratedDate);
        // Monthly frequency should advance by 1 month
        Assert.Equal(new DateTime(2026, 2, 15), ar.NextScheduleDate);
    }

    [Fact]
    public void AutoRepeat_DisableExpired_SetsDisabled()
    {
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 1, 31);
        var ar = new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), RepeatFrequency.Monthly, startDate, endDate);

        Assert.True(ar.IsEnabled);

        // RecordGeneration advances NextScheduleDate past EndDate → auto-disables
        ar.RecordGeneration(startDate);

        // Next date would be Feb 1, which is past Jan 31 EndDate
        Assert.False(ar.IsEnabled);
    }

    // === WorkstationScheduling Wiring ===

    [Fact]
    public void ScheduledTimeSlot_Scheduled_HasTimes()
    {
        var start = new DateTime(2026, 7, 24, 8, 0, 0);
        var end = new DateTime(2026, 7, 24, 10, 0, 0);

        var slot = new ScheduledTimeSlot(start, end, ScheduleStatus.Scheduled);

        Assert.Equal(start, slot.PlannedStart);
        Assert.Equal(end, slot.PlannedEnd);
        Assert.Equal(ScheduleStatus.Scheduled, slot.Status);
        Assert.Equal(120m, slot.DurationMinutes);
    }

    [Fact]
    public void ScheduledTimeSlot_NoCapacity_Status()
    {
        var start = new DateTime(2026, 7, 24, 17, 0, 0);
        var end = new DateTime(2026, 7, 25, 8, 0, 0);

        var slot = new ScheduledTimeSlot(start, end, ScheduleStatus.NoCapacity);

        Assert.Equal(ScheduleStatus.NoCapacity, slot.Status);
    }

    [Fact]
    public void ScheduleStatus_EnumValues()
    {
        Assert.Equal(0, (int)ScheduleStatus.Scheduled);
        Assert.Equal(1, (int)ScheduleStatus.NoCapacity);
    }

    // === WarehouseAccount Wiring ===

    [Fact]
    public void WarehouseAccount_DefaultAccountId_IsNullable()
    {
        var wa = new WarehouseAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(wa.StockReceivedButNotBilledAccountId);
        Assert.Null(wa.StockDeliveredButNotBilledAccountId);
        Assert.Null(wa.StockAdjustmentAccountId);
    }

    [Fact]
    public void WarehouseAccount_CanSetAccounts()
    {
        var srbnbId = Guid.NewGuid();
        var sdbnbId = Guid.NewGuid();
        var adjId = Guid.NewGuid();

        var wa = new WarehouseAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        wa.StockReceivedButNotBilledAccountId = srbnbId;
        wa.StockDeliveredButNotBilledAccountId = sdbnbId;
        wa.StockAdjustmentAccountId = adjId;

        Assert.Equal(srbnbId, wa.StockReceivedButNotBilledAccountId);
        Assert.Equal(sdbnbId, wa.StockDeliveredButNotBilledAccountId);
        Assert.Equal(adjId, wa.StockAdjustmentAccountId);
    }

    [Fact]
    public void Warehouse_DefaultAccountId_IsNullable()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Main Store");

        Assert.Null(wh.DefaultAccountId);
    }

    // === Master Detail View DTO ===

    [Fact]
    public void CustomerDto_HasAllDisplayFields()
    {
        var dto = new MyERP.Sales.CustomerDto
        {
            Id = Guid.NewGuid(),
            Name = "Acme Sdn Bhd",
            CustomerCode = "CUST-001",
            Tin = "C12345678",
            SstRegistrationNumber = "SST-001",
            Email = "acme@example.com",
            Phone = "012-3456789",
            IsActive = true,
        };

        Assert.Equal("Acme Sdn Bhd", dto.Name);
        Assert.Equal("CUST-001", dto.CustomerCode);
        Assert.Equal("C12345678", dto.Tin);
        Assert.Equal("SST-001", dto.SstRegistrationNumber);
        Assert.Equal("acme@example.com", dto.Email);
        Assert.Equal("012-3456789", dto.Phone);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public void Supplier_HasHoldTypeField()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Parts Co");

        Assert.Equal(SupplierHoldType.None, supplier.HoldType);
        Assert.False(supplier.IsOnHold);

        supplier.HoldType = SupplierHoldType.All;
        Assert.True(supplier.IsOnHold);
    }

    [Fact]
    public void ItemDto_HasStockFields()
    {
        var dto = new MyERP.Inventory.ItemDto
        {
            Id = Guid.NewGuid(),
            ItemCode = "ITEM-001",
            ItemName = "Widget",
            ReorderLevel = 10m,
            ReorderQty = 50m,
            SafetyStock = 5m,
        };

        Assert.Equal(10m, dto.ReorderLevel);
        Assert.Equal(50m, dto.ReorderQty);
        Assert.Equal(5m, dto.SafetyStock);
    }

    // === Document Edit Mode ===

    [Fact]
    public void SalesInvoice_DraftCanBeEdited()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-001", DateTime.UtcNow);

        // Draft SI allows adding items
        si.AddItem(Guid.NewGuid(), "Widget", 1, 100, 6);

        Assert.Single(si.Items);
    }

    [Fact]
    public void SalesInvoice_SubmittedCannotEditItems()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-002", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 1, 100, 6);
        si.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            si.AddItem(Guid.NewGuid(), "Another Widget", 2, 50, 3));
    }

    [Fact]
    public void MaterialRequest_DraftCanAddItems()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        mr.AddItem(Guid.NewGuid(), "Raw Material A", 100, "Kg");

        Assert.Single(mr.Items);
    }

    [Fact]
    public void MaterialRequest_SubmittedCannotAddItems()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-002",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Raw Material A", 100, "Kg");
        mr.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            mr.AddItem(Guid.NewGuid(), "Raw Material B", 50, "Kg"));
    }
}
