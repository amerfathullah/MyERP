using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for entity properties and methods identified as untested in the gap analysis.
/// Covers: SI computed properties, WO dates/unstop, PE references, PO lifecycle, amendment,
/// e-invoice, loyalty, consolidation, material transfer status transition, conversion flow prereqs,
/// SE value tracking, and MR→SO linkage.
/// </summary>
public class ConversionAndUntestedEntityTests
{
    private static readonly Guid _companyId = Guid.NewGuid();
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _supplierId = Guid.NewGuid();

    // ═══════ SalesInvoice: BaseOutstandingAmount multi-currency ═══════

    [Fact]
    public void SI_BaseOutstandingAmount_WithExchangeRate_CalculatesCorrectly()
    {
        var si = CreateSalesInvoice();
        si.ExchangeRate = 4.72m;
        si.AddItem(Guid.NewGuid(), "Item A", 1, 100m, 0m);
        // BaseGrandTotal = 100 * 4.72 = 472
        // BaseOutstandingAmount = BaseGrandTotal - (AmountPaid * ExchangeRate) = 472 - 0 = 472
        Assert.Equal(472m, si.BaseOutstandingAmount);
    }

    [Fact]
    public void SI_BaseOutstandingAmount_ReducedByPayment()
    {
        var si = CreateSalesInvoice();
        si.ExchangeRate = 1m;
        si.AddItem(Guid.NewGuid(), "Item", 1, 500m, 0m);
        si.AmountPaid = 200m;
        Assert.Equal(300m, si.BaseOutstandingAmount);
    }

    // ═══════ SalesInvoice: IsOverdue computed property ═══════

    [Fact]
    public void SI_IsOverdue_PostedPastDue_ReturnsTrue()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
        si.Submit();
        si.Post();
        si.DueDate = DateTime.UtcNow.AddDays(-5);
        Assert.True(si.IsOverdue);
    }

    [Fact]
    public void SI_IsOverdue_FutureDueDate_ReturnsFalse()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
        si.Submit();
        si.Post();
        si.DueDate = DateTime.UtcNow.AddDays(30);
        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SI_IsOverdue_FullyPaid_ReturnsFalse()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
        si.Submit();
        si.Post();
        si.DueDate = DateTime.UtcNow.AddDays(-5);
        si.AmountPaid = si.GrandTotal;
        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SI_IsOverdue_Return_NeverOverdue()
    {
        var si = CreateSalesInvoice();
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Credit Note Item", -1, 100m, 0m);
        si.Submit();
        si.Post();
        si.DueDate = DateTime.UtcNow.AddDays(-5);
        Assert.False(si.IsOverdue);
    }

    [Fact]
    public void SI_IsOverdue_NoDueDate_ReturnsFalse()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
        si.Submit();
        si.Post();
        si.DueDate = null;
        Assert.False(si.IsOverdue);
    }

    // ═══════ SalesInvoice: Loyalty Points ═══════

    [Fact]
    public void SI_LoyaltyPointsEarned_DefaultsZero()
    {
        var si = CreateSalesInvoice();
        Assert.Equal(0, si.LoyaltyPointsEarned);
        Assert.Equal(0, si.LoyaltyPointsRedeemed);
        Assert.Equal(0m, si.LoyaltyRedemptionAmount);
    }

    [Fact]
    public void SI_LoyaltyFields_CanBeSet()
    {
        var si = CreateSalesInvoice();
        si.LoyaltyPointsEarned = 150;
        si.LoyaltyPointsRedeemed = 50;
        si.LoyaltyRedemptionAmount = 25.50m;
        si.LoyaltyProgramId = Guid.NewGuid();

        Assert.Equal(150, si.LoyaltyPointsEarned);
        Assert.Equal(50, si.LoyaltyPointsRedeemed);
        Assert.Equal(25.50m, si.LoyaltyRedemptionAmount);
        Assert.NotNull(si.LoyaltyProgramId);
    }

    // ═══════ SalesInvoice: Amendment tracking ═══════

    [Fact]
    public void SI_AmendmentFields_DefaultsNull()
    {
        var si = CreateSalesInvoice();
        Assert.Null(si.AmendedFromId);
        Assert.Equal(0, si.AmendmentIndex);
    }

    [Fact]
    public void SI_AmendmentFields_CanBeSet()
    {
        var si = CreateSalesInvoice();
        var originalId = Guid.NewGuid();
        si.AmendedFromId = originalId;
        si.AmendmentIndex = 2;
        Assert.Equal(originalId, si.AmendedFromId);
        Assert.Equal(2, si.AmendmentIndex);
    }

    // ═══════ SalesInvoice: POS Consolidation ═══════

    [Fact]
    public void SI_ConsolidatedSalesInvoiceId_DefaultsNull()
    {
        var si = CreateSalesInvoice();
        Assert.Null(si.ConsolidatedSalesInvoiceId);
    }

    [Fact]
    public void SI_ConsolidatedSalesInvoiceId_CanBeSet()
    {
        var si = CreateSalesInvoice();
        var id = Guid.NewGuid();
        si.ConsolidatedSalesInvoiceId = id;
        Assert.Equal(id, si.ConsolidatedSalesInvoiceId);
    }

    // ═══════ SalesInvoice: E-Invoice ═══════

    [Fact]
    public void SI_EInvoiceStatus_DefaultsNotSubmitted()
    {
        var si = CreateSalesInvoice();
        Assert.Equal(EInvoiceStatus.NotSubmitted, si.EInvoiceStatus);
    }

    // ═══════ SalesInvoice: WriteOff ═══════

    [Fact]
    public void SI_SetWriteOff_ReducesOutstanding()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000m, 0m);
        si.SetWriteOff(100m);
        Assert.Equal(100m, si.WriteOffAmount);
        Assert.Equal(900m, si.OutstandingAmount);
    }

    [Fact]
    public void SI_SetWriteOff_WithAccounts()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000m, 0m);
        var acctId = Guid.NewGuid();
        var ccId = Guid.NewGuid();
        si.SetWriteOff(200m, acctId, ccId);
        Assert.Equal(acctId, si.WriteOffAccountId);
        Assert.Equal(ccId, si.WriteOffCostCenterId);
    }

    // ═══════ SalesInvoice: Advance ═══════

    [Fact]
    public void SI_SetTotalAdvance_ReducesOutstanding()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 1000m, 0m);
        si.SetTotalAdvance(300m);
        Assert.Equal(300m, si.TotalAdvance);
        Assert.Equal(700m, si.OutstandingAmount);
    }

    // ═══════ SalesInvoice: ApplyRounding ═══════

    [Fact]
    public void SI_ApplyRounding_RoundsToWholeNumber()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 99.50m, 0m);
        si.DisableRoundedTotal = false;
        si.ApplyRounding();
        Assert.Equal(Math.Round(si.GrandTotal), si.RoundedTotal);
    }

    [Fact]
    public void SI_ApplyRounding_Disabled_SetsRoundedTotalToGrandTotal()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Item", 1, 99.50m, 0m);
        si.DisableRoundedTotal = true;
        si.ApplyRounding();
        // When disabled, RoundedTotal = GrandTotal (no rounding applied), RoundingAdjustment = 0
        Assert.Equal(si.GrandTotal, si.RoundedTotal);
        Assert.Equal(0m, si.RoundingAdjustment);
    }

    // ═══════ WorkOrder: SetPlannedDates ═══════

    [Fact]
    public void WO_SetPlannedDates_ValidRange_Succeeds()
    {
        var wo = CreateWorkOrder();
        var start = DateTime.UtcNow;
        var end = start.AddDays(10);
        wo.SetPlannedDates(start, end);
        Assert.Equal(start, wo.PlannedStartDate);
        Assert.Equal(end, wo.PlannedEndDate);
    }

    [Fact]
    public void WO_SetPlannedDates_EndBeforeStart_Throws()
    {
        var wo = CreateWorkOrder();
        Assert.Throws<BusinessException>(() =>
            wo.SetPlannedDates(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void WO_SetPlannedDates_NullDates_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.SetPlannedDates(null, null);
        Assert.Null(wo.PlannedStartDate);
        Assert.Null(wo.PlannedEndDate);
    }

    // ═══════ WorkOrder: ValidateDates ═══════

    [Fact]
    public void WO_ValidateDates_InvalidActualRange_Throws()
    {
        var wo = CreateWorkOrder();
        wo.ActualStartDate = DateTime.UtcNow;
        wo.ActualEndDate = DateTime.UtcNow.AddDays(-2);
        Assert.Throws<BusinessException>(() => wo.ValidateDates());
    }

    [Fact]
    public void WO_ValidateDates_ValidActualRange_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.ActualStartDate = DateTime.UtcNow.AddDays(-5);
        wo.ActualEndDate = DateTime.UtcNow;
        wo.ValidateDates(); // Should not throw
    }

    // ═══════ WorkOrder: Unstop lifecycle ═══════

    [Fact]
    public void WO_Unstop_FromStopped_ReturnsToInProcess()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Unstop();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WO_Unstop_FromNonStopped_Throws()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        Assert.Throws<BusinessException>(() => wo.Unstop());
    }

    [Fact]
    public void WO_Cancel_FromStopped_MustUnstopFirst()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.Stop();
        Assert.Throws<BusinessException>(() => wo.Cancel());
    }

    [Fact]
    public void WO_Unstop_Then_Cancel_Succeeds()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Unstop();
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    // ═══════ WorkOrder: RecordMaterialTransfer status transition ═══════

    [Fact]
    public void WO_RecordMaterialTransfer_FromSubmitted_TransitionsToNotStarted()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.RecordMaterialTransfer(10m);
        Assert.Equal(WorkOrderStatus.NotStarted, wo.Status);
        Assert.Equal(10m, wo.MaterialTransferred);
    }

    [Fact]
    public void WO_RecordMaterialTransfer_FromInProcess_StaysInProcess()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.RecordMaterialTransfer(5m);
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WO_RecordMaterialTransfer_Cumulative()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.RecordMaterialTransfer(5m);
        wo.RecordMaterialTransfer(3m);
        Assert.Equal(8m, wo.MaterialTransferred);
    }

    // ═══════ WorkOrder: ScrapWarehouseId ═══════

    [Fact]
    public void WO_ScrapWarehouseId_DefaultsNull()
    {
        var wo = CreateWorkOrder();
        Assert.Null(wo.ScrapWarehouseId);
    }

    [Fact]
    public void WO_ScrapWarehouseId_CanBeSet()
    {
        var wo = CreateWorkOrder();
        var whId = Guid.NewGuid();
        wo.ScrapWarehouseId = whId;
        Assert.Equal(whId, wo.ScrapWarehouseId);
    }

    // ═══════ PurchaseOrder: Close/Reopen lifecycle ═══════

    [Fact]
    public void PO_Close_And_Reopen_Cycle()
    {
        var po = CreatePurchaseOrder();
        po.Submit();
        po.Close();
        Assert.Equal(DocumentStatus.Closed, po.Status);
        po.Reopen();
        Assert.NotEqual(DocumentStatus.Closed, po.Status);
    }

    [Fact]
    public void PO_Reopen_FromNonClosed_Throws()
    {
        var po = CreatePurchaseOrder();
        po.Submit();
        Assert.Throws<BusinessException>(() => po.Reopen());
    }

    // ═══════ PurchaseOrder: IsSubcontracted ═══════

    [Fact]
    public void PO_IsSubcontracted_DefaultsFalse()
    {
        var po = CreatePurchaseOrder();
        Assert.False(po.IsSubcontracted);
    }

    [Fact]
    public void PO_IsSubcontracted_CanBeSet()
    {
        var po = CreatePurchaseOrder();
        po.IsSubcontracted = true;
        Assert.True(po.IsSubcontracted);
    }

    // ═══════ StockEntry: Value Tracking ═══════

    [Fact]
    public void SE_TotalIncomingValue_SumsTargetWarehouseItems()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        var targetWh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 10, null, targetWh, 50m);
        Assert.Equal(500m, se.TotalIncomingValue);
    }

    [Fact]
    public void SE_TotalOutgoingValue_SumsSourceWarehouseItems()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialIssue, DateTime.UtcNow);
        var sourceWh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 5, sourceWh, null, 30m);
        Assert.Equal(150m, se.TotalOutgoingValue);
    }

    [Fact]
    public void SE_TotalValueDifference_BalancedTransfer_IsZero()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        var srcWh = Guid.NewGuid();
        var tgtWh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 10, srcWh, tgtWh, 25m);
        Assert.Equal(0m, se.TotalValueDifference);
    }

    [Fact]
    public void SE_EmptyEntry_AllValuesZero()
    {
        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        Assert.Equal(0m, se.TotalIncomingValue);
        Assert.Equal(0m, se.TotalOutgoingValue);
        Assert.Equal(0m, se.TotalValueDifference);
    }

    // ═══════ PaymentEntry: CostCenterId and ProjectId ═══════

    [Fact]
    public void PE_CostCenterId_DefaultsNull()
    {
        var pe = CreatePaymentEntry();
        Assert.Null(pe.CostCenterId);
        Assert.Null(pe.ProjectId);
    }

    [Fact]
    public void PE_CostCenterId_CanBeSet()
    {
        var pe = CreatePaymentEntry();
        var ccId = Guid.NewGuid();
        var projId = Guid.NewGuid();
        pe.CostCenterId = ccId;
        pe.ProjectId = projId;
        Assert.Equal(ccId, pe.CostCenterId);
        Assert.Equal(projId, pe.ProjectId);
    }

    // ═══════ MR: SalesOrderId on items ═══════

    [Fact]
    public void MRItem_SalesOrderId_DefaultsNull()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Item", 10, "Unit");
        Assert.Null(mr.Items.First().SalesOrderId);
    }

    [Fact]
    public void MRItem_SalesOrderId_CanBeSet()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Item", 10, "Unit");
        var soId = Guid.NewGuid();
        mr.Items.First().SalesOrderId = soId;
        Assert.Equal(soId, mr.Items.First().SalesOrderId);
    }

    // ═══════ SO→MR conversion prerequisites ═══════

    [Fact]
    public void SO_PendingDeliveryQty_FullQtyWhenNoDelivery()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 50m, 0m);
        so.Submit();
        Assert.Equal(10m, so.Items.First().PendingDeliveryQty);
    }

    [Fact]
    public void SO_PendingDeliveryQty_ReducedAfterPartialDelivery()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-002", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item B", 10, 50m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 6m;
        Assert.Equal(4m, so.Items.First().PendingDeliveryQty);
    }

    [Fact]
    public void SO_PendingDeliveryQty_ZeroWhenFullyDelivered()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-003", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item C", 10, 50m, 0m);
        so.Submit();
        so.Items.First().DeliveredQty = 10m;
        Assert.Equal(0m, so.Items.First().PendingDeliveryQty);
    }

    // ═══════ Helpers ═══════

    private static SalesInvoice CreateSalesInvoice()
    {
        return new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SI-TEST-001", DateTime.UtcNow);
    }

    private static WorkOrder CreateWorkOrder()
    {
        return new WorkOrder(Guid.NewGuid(), _companyId, "WO-TEST-001",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
    }

    private static PurchaseOrder CreatePurchaseOrder()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-TEST-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test Item", 10, 100m, 0m);
        return po;
    }

    private static PaymentEntry CreatePaymentEntry()
    {
        return new PaymentEntry(Guid.NewGuid(), _companyId,
            PaymentType.Receive, DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());
    }
}
