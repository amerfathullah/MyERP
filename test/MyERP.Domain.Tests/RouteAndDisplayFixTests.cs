using System;
using System.Linq;
using Xunit;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Accounting.Entities;
using MyERP.Workflow.Entities;
using MyERP.Workflow;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing;
using MyERP.Core;

namespace MyERP.DomainTests;

/// <summary>
/// Tests covering route registration fixes, GUID→name display resolution,
/// and entity properties used by detail components and list name lookups.
/// </summary>
public class RouteAndDisplayFixTests
{
    // === POS Closing — detail route entity support ===

    [Fact]
    public void PosClosingEntry_DefaultStatus()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosClosingStatus.Draft, entry.Status);
    }

    [Fact]
    public void PosClosingEntry_PaymentVariance_CalculatedFromExpectedVsActual()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", 1000m, 980m);
        // Expected - Closing = 1000 - 980 = 20 (short)
        Assert.Equal(20m, entry.TotalDifference);
    }

    [Fact]
    public void PosClosingEntry_GrandTotal_DefaultsZero()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0m, entry.GrandTotal);
    }

    // === POS Opening — detail route entity support ===

    [Fact]
    public void PosOpeningEntry_DefaultOpen()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosOpeningStatus.Open, entry.Status);
    }

    [Fact]
    public void PosOpeningEntry_TotalOpeningAmount_SumsPayments()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddOpeningBalance(Guid.NewGuid(), "Cash", 500m);
        entry.AddOpeningBalance(Guid.NewGuid(), "Card", 300m);
        Assert.Equal(800m, entry.TotalOpeningAmount);
    }

    // === Pick List — detail route + list navigation ===

    [Fact]
    public void PickList_DefaultDraft()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        Assert.Equal(DocumentStatus.Draft, pl.Status);
    }

    [Fact]
    public void PickList_AddItem_TracksQty()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        Assert.Single(pl.Items);
        Assert.Equal(10m, pl.Items.First().Qty);
    }

    [Fact]
    public void PickList_CustomerId_CanBeSetForDirectDelivery()
    {
        var customerId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.CustomerId = customerId;
        Assert.Equal(customerId, pl.CustomerId);
    }

    // === Stock Closing — detail route entity support ===

    [Fact]
    public void StockClosingEntry_DefaultDraft()
    {
        var sce = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(StockClosingStatus.Draft, sce.Status);
    }

    [Fact]
    public void StockClosingEntry_AddBalance_IncrementsCount()
    {
        var sce = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        sce.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m, null);
        Assert.Single(sce.Balances);
    }

    // === Cost Center Allocation — detail route support ===

    [Fact]
    public void CostCenterAllocation_EvenDistribution()
    {
        var cca = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        cca.AddEntry(Guid.NewGuid(), 50m);
        cca.AddEntry(Guid.NewGuid(), 50m);
        var result = cca.Distribute(1000m);
        Assert.Equal(2, result.Count);
        Assert.Equal(500m, result[0].Amount);
        Assert.Equal(500m, result[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_UnevenDistribution_RemainderToFirst()
    {
        var cca = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        cca.AddEntry(Guid.NewGuid(), 33.33m);
        cca.AddEntry(Guid.NewGuid(), 33.33m);
        cca.AddEntry(Guid.NewGuid(), 33.34m);
        var result = cca.Distribute(100m);
        Assert.Equal(3, result.Count);
        // Total must equal 100 exactly
        Assert.Equal(100m, result.Sum(r => r.Amount));
    }

    // === Supplier Scorecard — name resolution support ===

    [Fact]
    public void SupplierScorecard_SupplierId_IsSet()
    {
        var supplierId = Guid.NewGuid();
        var sc = new SupplierScorecard(Guid.NewGuid(), supplierId, Guid.NewGuid());
        Assert.Equal(supplierId, sc.SupplierId);
    }

    [Fact]
    public void SupplierScorecard_DefaultScore_Is100()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(100m, sc.Score);
    }

    [Fact]
    public void SupplierScorecard_ScoreUpdate_ClampsTo0_100()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        sc.UpdateScore(150m);
        Assert.Equal(100m, sc.Score);
        sc.UpdateScore(-10m);
        Assert.Equal(0m, sc.Score);
    }

    // === Approval Request — user ID used for display ===

    [Fact]
    public void ApprovalRequest_RequestedByUserId_IsTracked()
    {
        var userId = Guid.NewGuid();
        var req = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 1, userId);
        Assert.Equal(userId, req.RequestedByUserId);
    }

    [Fact]
    public void ApprovalRequest_DefaultPending()
    {
        var req = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 1, Guid.NewGuid());
        Assert.Equal(ApprovalStatus.Pending, req.Status);
    }

    [Fact]
    public void ApprovalRequest_Level_IsSet()
    {
        var req = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(), "PurchaseOrder", Guid.NewGuid(), 3, Guid.NewGuid());
        Assert.Equal(3, req.Level);
    }

    // === Work Order — production tracking for WO detail ===

    [Fact]
    public void WorkOrder_PercentComplete_FromProducedQty()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(40m);
        Assert.Equal(40m, wo.PercentComplete);
    }

    [Fact]
    public void WorkOrder_PercentComplete_ZeroQty_NoException()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 0m);
        // Zero quantity should not throw division-by-zero
        Assert.Equal(0m, wo.PercentComplete);
    }

    // === Financial Report Template — entity for template list ===

    [Fact]
    public void FinancialReportTemplate_DefaultEnabled()
    {
        var frt = new FinancialReportTemplate(Guid.NewGuid(), "P&L Report", FinancialReportType.ProfitAndLoss);
        Assert.True(frt.IsEnabled);
    }

    [Fact]
    public void FinancialReportTemplate_CanDisable()
    {
        var frt = new FinancialReportTemplate(Guid.NewGuid(), "BS Report", FinancialReportType.BalanceSheet);
        frt.Disable();
        Assert.False(frt.IsEnabled);
    }
}
