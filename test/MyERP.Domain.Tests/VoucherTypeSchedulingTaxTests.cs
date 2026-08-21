using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using MyERP.Sales.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for JournalEntry voucher type validation, Workstation scheduling,
/// SI advance auto-adjustment, and inclusive tax exclusive rate calculation.
/// </summary>
public class VoucherTypeSchedulingTaxTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();

    // === JournalEntry VoucherType Tests ===

    [Fact]
    public void JournalEntryVoucherType_DefaultIsJournalEntry()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType.ShouldBe(JournalEntryVoucherType.JournalEntry);
    }

    [Fact]
    public void JournalEntryVoucherType_CanBeSetToAllTypes()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);

        je.VoucherType = JournalEntryVoucherType.BankEntry;
        je.VoucherType.ShouldBe(JournalEntryVoucherType.BankEntry);

        je.VoucherType = JournalEntryVoucherType.OpeningEntry;
        je.VoucherType.ShouldBe(JournalEntryVoucherType.OpeningEntry);

        je.VoucherType = JournalEntryVoucherType.DepreciationEntry;
        je.VoucherType.ShouldBe(JournalEntryVoucherType.DepreciationEntry);

        je.VoucherType = JournalEntryVoucherType.ExchangeRateRevaluation;
        je.VoucherType.ShouldBe(JournalEntryVoucherType.ExchangeRateRevaluation);
    }

    [Fact]
    public void JournalEntryVoucherType_Enum_Has19Values()
    {
        // 18 + PaymentTax (added so a Payment Entry's tax JE can be found and reversed
        // independently of its main GL JE on cancel/repost — see
        // DocumentPostingOrchestrator.ReversePaymentTaxJournalEntriesAsync).
        var values = Enum.GetValues<JournalEntryVoucherType>();
        values.Length.ShouldBe(19);
    }

    [Fact]
    public void JournalEntry_OpeningEntry_SetsIsOpening()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.OpeningEntry;

        var creditId = Guid.NewGuid();
        je.AddLine(_accountId, 1000m, true);
        je.AddLine(creditId, 1000m, false);

        je.Post();

        // Opening entry auto-forces IsOpening = true
        je.IsOpening.ShouldBeTrue();
        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void JournalEntry_Reversal_RequiresReversalOfId()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.Reversal;

        je.AddLine(_accountId, 500m, true);
        je.AddLine(Guid.NewGuid(), 500m, false);

        // Reversal without ReversalOfId should throw
        Should.Throw<Volo.Abp.BusinessException>(() => je.Post());
    }

    [Fact]
    public void JournalEntry_Reversal_WithReversalOfId_Succeeds()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.Reversal;
        je.ReversalOfId = Guid.NewGuid(); // reference to original JE

        je.AddLine(_accountId, 500m, true);
        je.AddLine(Guid.NewGuid(), 500m, false);

        je.Post();
        je.Status.ShouldBe(DocumentStatus.Posted);
        je.ReversalOfId.ShouldNotBeNull();
    }

    [Fact]
    public void JournalEntry_DepreciationEntry_PostSucceeds()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.DepreciationEntry;

        je.AddLine(_accountId, 2000m, true); // depreciation expense
        je.AddLine(Guid.NewGuid(), 2000m, false); // accumulated depreciation

        je.Post();
        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void JournalEntry_IsMultiCurrency_DefaultsFalse()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.IsMultiCurrency.ShouldBeFalse();
    }

    [Fact]
    public void JournalEntry_InterCompanyJournalEntryId_DefaultsNull()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.InterCompanyJournalEntryId.ShouldBeNull();
    }

    // === Workstation Scheduling Tests ===

    [Fact]
    public void ScheduledTimeSlot_DurationCalculation()
    {
        var start = new DateTime(2026, 7, 24, 8, 0, 0);
        var end = new DateTime(2026, 7, 24, 10, 30, 0);
        var slot = new ScheduledTimeSlot(start, end, ScheduleStatus.Scheduled);

        slot.DurationMinutes.ShouldBe(150m); // 2.5 hours
        slot.Status.ShouldBe(ScheduleStatus.Scheduled);
    }

    [Fact]
    public void ScheduledTimeSlot_NoCapacity_Status()
    {
        var slot = new ScheduledTimeSlot(
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2), ScheduleStatus.NoCapacity);
        slot.Status.ShouldBe(ScheduleStatus.NoCapacity);
    }

    [Fact]
    public void ScheduleStatus_Enum_HasCorrectValues()
    {
        ((int)ScheduleStatus.Scheduled).ShouldBe(0);
        ((int)ScheduleStatus.NoCapacity).ShouldBe(1);
    }

    [Fact]
    public void Workstation_WorkingHours_AddValid()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "WS-01");
        ws.AddWorkingHour("Monday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        ws.WorkingHours.Count.ShouldBe(1);
        ws.WorkingHours[0].Day.ShouldBe("Monday");
    }

    [Fact]
    public void Workstation_WorkingHours_StartAfterEnd_Throws()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "WS-01");
        Should.Throw<ArgumentException>(() =>
            ws.AddWorkingHour("Monday", new TimeSpan(17, 0, 0), new TimeSpan(8, 0, 0)));
    }

    [Fact]
    public void Workstation_ProductionCapacity_Default()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "WS-01");
        ws.ProductionCapacity.ShouldBe(1);
    }

    [Fact]
    public void Workstation_HourRate_CalculatesFromCosts()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "WS-01");
        ws.AddCost("Labor", 30m);
        ws.AddCost("Electricity", 10m);
        ws.HourRate.ShouldBe(40m);
    }

    // === Inclusive Tax Exclusive Rate Tests ===

    [Fact]
    public void TaxCalculation_InclusiveTax_BackCalculatesExclusiveRate()
    {
        var service = new TaxesAndTotalsService();

        // Item priced at 106 (inclusive of 6% SST)
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 106m, NetAmount = 106m }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST", "On Net Total", 6m)
            {
                TaxCategory = "Total",
                IncludedInPrintRate = true,
                AccountId = Guid.NewGuid()
            }
        };

        var result = service.Calculate(items, taxes);

        // Exclusive rate = 106 / (1 + 0.06) = 100
        // Tax = 100 × 6% = 6
        // NetTotal = 100
        result.NetTotal.ShouldBe(100m);
        result.TotalTax.ShouldBe(6m);
        result.GrandTotal.ShouldBe(106m);
    }

    [Fact]
    public void TaxCalculation_InclusiveTax_MultiItemsDistribution()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 2, Rate = 53m, NetAmount = 106m }, // 2 × 53 = 106 inclusive
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 212m, NetAmount = 212m }  // 1 × 212 inclusive
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST", "On Net Total", 6m)
            {
                TaxCategory = "Total",
                IncludedInPrintRate = true,
                AccountId = Guid.NewGuid()
            }
        };

        var result = service.Calculate(items, taxes);

        // Total inclusive = 318
        // Exclusive = 318 / 1.06 = 300
        result.NetTotal.ShouldBe(300m);
        result.GrandTotal.ShouldBe(318m);
    }

    [Fact]
    public void TaxCalculation_NonInclusiveTax_NoBackCalculation()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 100m, NetAmount = 100m }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST", "On Net Total", 6m)
            {
                TaxCategory = "Total",
                IncludedInPrintRate = false, // NOT inclusive
                AccountId = Guid.NewGuid()
            }
        };

        var result = service.Calculate(items, taxes);

        // NetAmount stays 100 (no back-calculation)
        result.NetTotal.ShouldBe(100m);
        result.TotalTax.ShouldBe(6m);
        result.GrandTotal.ShouldBe(106m);
    }

    [Fact]
    public void TaxCalculation_InclusiveTax_ZeroRate_NoChange()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 100m, NetAmount = 100m }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "Exempt", "On Net Total", 0m)
            {
                TaxCategory = "Total",
                IncludedInPrintRate = true,
                AccountId = Guid.NewGuid()
            }
        };

        var result = service.Calculate(items, taxes);

        // 0% inclusive tax = no adjustment
        result.NetTotal.ShouldBe(100m);
        result.TotalTax.ShouldBe(0m);
        result.GrandTotal.ShouldBe(100m);
    }

    [Fact]
    public void TaxCalculation_InclusiveTax_10Percent()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 110m, NetAmount = 110m }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SalesTax", "On Net Total", 10m)
            {
                TaxCategory = "Total",
                IncludedInPrintRate = true,
                AccountId = Guid.NewGuid()
            }
        };

        var result = service.Calculate(items, taxes);

        // 110 / 1.10 = 100
        result.NetTotal.ShouldBe(100m);
        result.TotalTax.ShouldBe(10m);
        result.GrandTotal.ShouldBe(110m);
    }

    [Fact]
    public void TransactionItem_InclusiveTaxAmount_DefaultsZero()
    {
        var item = new TransactionItem
        {
            ItemId = Guid.NewGuid(), Qty = 1, Rate = 100m, NetAmount = 100m
        };
        item.InclusiveTaxAmount.ShouldBe(0m);
    }

    // === Sales Invoice TotalAdvance Tests ===

    [Fact]
    public void SalesInvoice_TotalAdvance_DefaultsZero()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.TotalAdvance.ShouldBe(0m);
    }

    [Fact]
    public void SalesInvoice_TotalAdvance_ReducesOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000m, 0);
        si.SetTotalAdvance(300m);

        si.GrandTotal.ShouldBe(1000m);
        si.TotalAdvance.ShouldBe(300m);
        si.OutstandingAmount.ShouldBe(700m); // 1000 - 0 paid - 0 writeoff - 300 advance
    }

    [Fact]
    public void SalesInvoice_TotalAdvance_WithPayment_CorrectOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000m, 0);
        si.SetTotalAdvance(300m);
        si.AmountPaid = 200m;

        si.OutstandingAmount.ShouldBe(500m); // 1000 - 200 - 0 - 300
    }

    // === Sales Order AdvancePaid Tests ===

    [Fact]
    public void SalesOrder_AdvancePaid_DefaultsZero()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001",
            DateTime.UtcNow);
        so.AdvancePaid.ShouldBe(0m);
    }

    [Fact]
    public void SalesOrder_PerAdvancePaid_Calculation()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-001",
            DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0);
        so.AdvancePaid = 500m;

        // PerAdvancePaid = 500 / 1000 × 100 = 50%
        so.PerAdvancePaid.ShouldBe(50m);
    }

    // === JournalEntry VoucherType Specific Rules ===

    [Fact]
    public void JournalEntry_PeriodClosing_PostSucceeds()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.PeriodClosing;

        je.AddLine(_accountId, 5000m, true);
        je.AddLine(Guid.NewGuid(), 5000m, false);

        je.Post();
        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void JournalEntry_ExchangeGainOrLoss_PostSucceeds()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.ExchangeGainOrLoss;

        je.AddLine(_accountId, 100m, true);
        je.AddLine(Guid.NewGuid(), 100m, false);

        je.Post();
        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void JournalEntry_ContraEntry_PostSucceeds()
    {
        var je = new JournalEntry(Guid.NewGuid(), _companyId, _fiscalYearId, DateTime.UtcNow);
        je.VoucherType = JournalEntryVoucherType.ContraEntry;

        var bankAccountId = Guid.NewGuid();
        var cashAccountId = Guid.NewGuid();
        je.AddLine(bankAccountId, 3000m, true);
        je.AddLine(cashAccountId, 3000m, false);

        je.Post();
        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    // === ManufacturingSettings for Scheduling ===

    [Fact]
    public void ManufacturingSettings_MinsBetweenOperations_Default()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.MinsBetweenOperations.ShouldBe(10);
    }

    [Fact]
    public void ManufacturingSettings_CapacityPlanningDays_Default()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.CapacityPlanningForDays.ShouldBe(30);
    }

    [Fact]
    public void ManufacturingSettings_AllowProductionOnHolidays_DefaultFalse()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.AllowProductionOnHolidays.ShouldBeFalse();
    }

    // === Discount Pipeline Tests ===

    [Fact]
    public void TaxCalculation_DiscountOnNetTotal_ReducesBeforeTax()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000m, NetAmount = 1000m }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST", "On Net Total", 6m)
            { TaxCategory = "Total" }
        };

        // 10% discount on Net Total
        var result = service.Calculate(items, taxes, discountAmount: 100m, applyDiscountOn: "Net Total");

        // Net = 1000 - 100 = 900
        // Tax = 900 × 6% = 54
        // Grand = 900 + 54 = 954
        result.NetTotal.ShouldBe(900m);
        result.TotalTax.ShouldBe(54m);
        result.GrandTotal.ShouldBe(954m);
    }

    [Fact]
    public void TaxCalculation_DiscountOnGrandTotal_ReducesAfterTax()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1, Rate = 1000m, NetAmount = 1000m }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(), 1, "SST", "On Net Total", 6m)
            { TaxCategory = "Total" }
        };

        // 100 discount on Grand Total
        var result = service.Calculate(items, taxes, discountAmount: 100m, applyDiscountOn: "Grand Total");

        // Net = 1000 (unchanged)
        // Tax = 60 (unchanged)
        // Grand = 1060 - 100 = 960
        result.NetTotal.ShouldBe(1000m);
        result.TotalTax.ShouldBe(60m);
        result.GrandTotal.ShouldBe(960m);
    }
}
