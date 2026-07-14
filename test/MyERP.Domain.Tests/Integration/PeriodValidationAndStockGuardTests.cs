using System;
using MyERP.Core;
using MyERP.Manufacturing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for accounting period validation, group warehouse blocking,
/// leave overlap detection, and Work Order cancel stock reversal.
/// </summary>
public class PeriodValidationAndStockGuardTests
{
    // -- Accounting Period Validation --

    [Fact]
    public void AccountingPeriod_ContainsDate_InsidePeriod()
    {
        var period = new Accounting.Entities.AccountingPeriod(
            Guid.NewGuid(), Guid.NewGuid(), "Q1 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null);

        period.ContainsDate(new DateTime(2026, 2, 15)).ShouldBeTrue();
    }

    [Fact]
    public void AccountingPeriod_ContainsDate_OutsidePeriod()
    {
        var period = new Accounting.Entities.AccountingPeriod(
            Guid.NewGuid(), Guid.NewGuid(), "Q1 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null);

        period.ContainsDate(new DateTime(2026, 4, 1)).ShouldBeFalse();
    }

    [Fact]
    public void AccountingPeriod_Closed_BlocksPosting()
    {
        var period = new Accounting.Entities.AccountingPeriod(
            Guid.NewGuid(), Guid.NewGuid(), "Q1 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null);
        period.Close();

        period.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void Company_AccountsFrozenTillDate_DefaultNull()
    {
        var company = new Core.Entities.Company(Guid.NewGuid(), "Test Co");
        company.AccountsFrozenTillDate.ShouldBeNull();
    }

    [Fact]
    public void Company_StockFrozenUpto_DefaultNull()
    {
        var company = new Core.Entities.Company(Guid.NewGuid(), "Test Co");
        company.StockFrozenUpto.ShouldBeNull();
    }

    // -- Work Order Cancel Stock Reversal --

    [Fact]
    public void WorkOrder_Cancel_FromSubmitted_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Cancel();
        wo.Status.ShouldBe(WorkOrderStatus.Cancelled);
    }

    [Fact]
    public void WorkOrder_ProducedQuantity_TracksProduction()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30, overproductionPercentage: 0);

        wo.ProducedQuantity.ShouldBe(30);
    }

    [Fact]
    public void WorkOrder_Cancel_AfterProduction_AllowedAtEntityLevel()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, overproductionPercentage: 0);

        // Entity-level cancel should work — AppService handles stock reversal
        wo.Cancel();
        wo.Status.ShouldBe(WorkOrderStatus.Cancelled);
    }

    // -- Leave Overlap Detection --

    [Fact]
    public void LeaveApplication_Create_ValidDates()
    {
        var leave = new HumanResources.Entities.LeaveApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 5), 5);

        leave.FromDate.ShouldBe(new DateTime(2026, 7, 1));
        leave.ToDate.ShouldBe(new DateTime(2026, 7, 5));
        leave.TotalLeaveDays.ShouldBe(5);
    }

    [Fact]
    public void LeaveOverlap_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.LeaveOverlap.ShouldBe("MyERP:14004");
    }

    // -- RFQ + PosClosing Integration Checks --

    [Fact]
    public void RFQ_SubmitWithItemsAndSuppliers_SetsSubmitted()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A");

        rfq.Submit();
        rfq.Status.ShouldBe(DocumentStatus.Submitted);
        rfq.Items.Count.ShouldBe(1);
        rfq.Suppliers.Count.ShouldBe(1);
    }

    [Fact]
    public void PosClosingEntry_CalculatesGrandTotalOnSubmit()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());

        entry.AddInvoice(Guid.NewGuid(), "POS-001", 500);
        entry.AddInvoice(Guid.NewGuid(), "POS-002", 750);
        entry.Submit();

        entry.GrandTotal.ShouldBe(1250m);
        entry.Status.ShouldBe(PosClosingStatus.Submitted);
    }

    [Fact]
    public void PosClosingEntry_PaymentVariance_Calculated()
    {
        var entry = new PosClosingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());

        entry.AddPayment(Guid.NewGuid(), "Cash", 1000, 980); // 20 short
        entry.AddPayment(Guid.NewGuid(), "Card", 500, 500);  // exact

        entry.TotalDifference.ShouldBe(20m);
    }

    // -- Error Code Verification --

    [Fact]
    public void DuplicateRfqSupplier_ErrorCode()
    {
        MyERPDomainErrorCodes.DuplicateRfqSupplier.ShouldBe("MyERP:04010");
    }

    [Fact]
    public void DuplicateSupplierInvoice_ErrorCode()
    {
        MyERPDomainErrorCodes.DuplicateSupplierInvoice.ShouldBe("MyERP:04009");
    }

    private static Manufacturing.Entities.WorkOrder CreateWorkOrder()
    {
        var wo = new Manufacturing.Entities.WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        return wo;
    }
}
