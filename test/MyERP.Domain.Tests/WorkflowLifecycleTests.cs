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
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Comprehensive workflow lifecycle tests covering complete document state machines.
/// Verifies: valid transitions, blocked transitions, side effects, edge cases.
/// </summary>
public class WorkflowLifecycleTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid FiscalYearId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();

    // ============================
    // Sales Invoice Lifecycle
    // ============================

    [Fact]
    public void SI_FullLifecycle_Draft_Submit_Post_Cancel()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-001", DateTime.Today);
        Assert.Equal(DocumentStatus.Draft, si.Status);

        si.AddItem(ItemId, "Widget", 5, 100, 0);
        si.Submit();
        Assert.Equal(DocumentStatus.Submitted, si.Status);

        si.Post();
        Assert.Equal(DocumentStatus.Posted, si.Status);

        si.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, si.Status);
    }

    [Fact]
    public void SI_CannotPostFromDraft()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-002", DateTime.Today);
        si.AddItem(ItemId, "Widget", 1, 100, 0);
        Assert.Throws<BusinessException>(() => si.Post());
    }

    [Fact]
    public void SI_CannotSubmitWithoutItems()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-003", DateTime.Today);
        Assert.Throws<BusinessException>(() => si.Submit());
    }

    [Fact]
    public void SI_CannotAddItemAfterSubmit()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-004", DateTime.Today);
        si.AddItem(ItemId, "Widget", 1, 100, 0);
        si.Submit();
        Assert.Throws<BusinessException>(() => si.AddItem(Guid.NewGuid(), "Extra", 1, 50, 0));
    }

    [Fact]
    public void SI_CreditNote_NegativeQty_Allowed()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-RET-001", DateTime.Today);
        si.IsReturn = true;
        si.ReturnAgainstId = Guid.NewGuid();
        si.AddItem(ItemId, "Widget", -3, 100, 0);
        Assert.Equal(-300m, si.GrandTotal);
    }

    [Fact]
    public void SI_Outstanding_ReducesWithPayment()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-005", DateTime.Today);
        si.AddItem(ItemId, "Widget", 10, 100, 0);
        si.Submit();
        si.Post();

        Assert.Equal(1000m, si.OutstandingAmount);
        si.AmountPaid = 400m;
        Assert.Equal(600m, si.OutstandingAmount);
    }

    // ============================
    // Purchase Invoice Lifecycle
    // ============================

    [Fact]
    public void PI_FullLifecycle()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-001", DateTime.Today);
        pi.AddItem(ItemId, "Raw Material", 20, 50, 0);
        Assert.Equal(DocumentStatus.Draft, pi.Status);

        pi.Submit();
        Assert.Equal(DocumentStatus.Submitted, pi.Status);

        pi.Post();
        Assert.Equal(DocumentStatus.Posted, pi.Status);

        pi.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, pi.Status);
    }

    [Fact]
    public void PI_DebitNote_NegativeQty()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-RET-001", DateTime.Today);
        pi.IsReturn = true;
        pi.ReturnAgainstId = Guid.NewGuid();
        pi.AddItem(ItemId, "Material", -5, 50, 0);
        Assert.True(pi.GrandTotal < 0);
    }

    // ============================
    // Sales Order Fulfillment Lifecycle
    // ============================

    [Fact]
    public void SO_Submit_TransitionsToDeliverAndBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-001", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0);
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SO_FullDelivery_ToBill()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-002", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0);
        so.Submit();

        so.Items[0].DeliveredQty = 10;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status);
    }

    [Fact]
    public void SO_FullDeliveryAndBilling_Completed()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-003", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0);
        so.Submit();

        so.Items[0].DeliveredQty = 10;
        so.Items[0].BilledQty = 10;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    [Fact]
    public void SO_Close_And_Reopen()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-004", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0);
        so.Submit();

        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);

        so.Reopen();
        // Reopened with no delivery = ToDeliverAndBill
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);
    }

    [Fact]
    public void SO_CannotCloseFromDraft()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-005", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0);
        Assert.Throws<BusinessException>(() => so.Close());
    }

    [Fact]
    public void SO_CannotReopenIfNotClosed()
    {
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-006", DateTime.Today);
        so.AddItem(ItemId, "Widget", 10, 100, 0);
        so.Submit();
        Assert.Throws<BusinessException>(() => so.Reopen());
    }

    // ============================
    // Purchase Order Fulfillment Lifecycle
    // ============================

    [Fact]
    public void PO_Submit_TransitionsToDeliverAndBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-001", DateTime.Today);
        po.AddItem(ItemId, "Material", 50, 20, 0);
        po.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    [Fact]
    public void PO_FullReceiptAndBilling_Completed()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-002", DateTime.Today);
        po.AddItem(ItemId, "Material", 50, 20, 0);
        po.Submit();

        po.Items[0].ReceivedQty = 50;
        po.Items[0].BilledQty = 50;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, po.Status);
    }

    [Fact]
    public void PO_PartialReceipt_StaysOpen()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-003", DateTime.Today);
        po.AddItem(ItemId, "A", 10, 100, 0);
        po.AddItem(Guid.NewGuid(), "B", 20, 50, 0);
        po.Submit();

        po.Items[0].ReceivedQty = 10; // 100%
        po.Items[1].ReceivedQty = 5;  // 25%
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status); // MIN(100%, 25%) = stays open
    }

    [Fact]
    public void PO_Close_Reopen_Cycle()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-004", DateTime.Today);
        po.AddItem(ItemId, "Material", 50, 20, 0);
        po.Submit();

        po.Close();
        Assert.Equal(DocumentStatus.Closed, po.Status);

        po.Reopen();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    // ============================
    // Stock Entry Lifecycle
    // ============================

    [Fact]
    public void SE_FullLifecycle()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        se.AddItem(ItemId, 100, null, WarehouseId, 50m);
        Assert.Equal(DocumentStatus.Draft, se.Status);

        se.Submit();
        Assert.Equal(DocumentStatus.Submitted, se.Status);

        se.Post();
        Assert.Equal(DocumentStatus.Posted, se.Status);

        se.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, se.Status);
    }

    [Fact]
    public void SE_CannotSubmitEmpty()
    {
        var se = new StockEntry(Guid.NewGuid(), CompanyId, StockEntryType.MaterialReceipt, DateTime.Today);
        Assert.Throws<BusinessException>(() => se.Submit());
    }

    [Fact]
    public void SE_AllTypes_Exist()
    {
        // Verify all 14 StockEntryType values are valid
        var types = Enum.GetValues<StockEntryType>();
        Assert.True(types.Length >= 14);
    }

    // ============================
    // Payment Entry Lifecycle
    // ============================

    [Fact]
    public void PE_FullLifecycle()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, Accounting.PaymentType.Receive,
            DateTime.Today, 5000, AccountId, Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, pe.Status);

        pe.Submit();
        Assert.Equal(DocumentStatus.Submitted, pe.Status);

        pe.Post();
        Assert.Equal(DocumentStatus.Posted, pe.Status);

        pe.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, pe.Status);
    }

    [Fact]
    public void PE_UnallocatedAmount_NoReferences()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), CompanyId, Accounting.PaymentType.Receive,
            DateTime.Today, 10000, AccountId, Guid.NewGuid());
        Assert.Equal(10000m, pe.UnallocatedAmount);
    }

    // ============================
    // Journal Entry Lifecycle
    // ============================

    [Fact]
    public void JE_PostDirectly_SkipsSubmit()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.Today);
        je.AddLine(AccountId, 1000, true);
        je.AddLine(Guid.NewGuid(), 1000, false);
        Assert.Equal(DocumentStatus.Draft, je.Status);

        je.Post(); // JE goes Draft → Posted (no Submit step)
        Assert.Equal(DocumentStatus.Posted, je.Status);
    }

    [Fact]
    public void JE_MustBeBalanced()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.Today);
        je.AddLine(AccountId, 1000, true);
        je.AddLine(Guid.NewGuid(), 500, false); // Unbalanced
        Assert.Throws<BusinessException>(() => je.Post());
    }

    [Fact]
    public void JE_EmptyLines_Blocked()
    {
        var je = new JournalEntry(Guid.NewGuid(), CompanyId, FiscalYearId, DateTime.Today);
        Assert.Throws<BusinessException>(() => je.Post());
    }

    // ============================
    // Delivery Note Lifecycle
    // ============================

    [Fact]
    public void DN_FullLifecycle()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-001", DateTime.Today);
        dn.AddItem(ItemId, "Widget", 10, 100, 0);
        Assert.Equal(DocumentStatus.Draft, dn.Status);

        dn.Submit();
        Assert.Equal(DocumentStatus.Submitted, dn.Status);

        dn.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, dn.Status);
    }

    [Fact]
    public void DN_Return_FlagsSet()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), CompanyId, CustomerId, WarehouseId, "DN-RET-001", DateTime.Today);
        dn.IsReturn = true;
        dn.ReturnAgainstId = Guid.NewGuid();
        Assert.True(dn.IsReturn);
        Assert.NotNull(dn.ReturnAgainstId);
    }

    // ============================
    // Purchase Receipt Lifecycle
    // ============================

    [Fact]
    public void PR_FullLifecycle()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.Today);
        pr.AddItem(ItemId, "Material", 50, 20, 0);
        Assert.Equal(DocumentStatus.Draft, pr.Status);

        pr.Submit();
        Assert.Equal(DocumentStatus.Submitted, pr.Status);

        pr.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, pr.Status);
    }

    // ============================
    // Work Order Lifecycle
    // ============================

    [Fact]
    public void WO_FullLifecycle()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        Assert.Equal(WorkOrderStatus.Draft, wo.Status);

        wo.Submit();
        Assert.Equal(WorkOrderStatus.Submitted, wo.Status);

        wo.Start();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);

        // Partial production — WO stays InProcess
        wo.RecordProduction(60);
        Assert.Equal(60m, wo.ProducedQuantity);
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);

        wo.Stop();
        Assert.Equal(WorkOrderStatus.Stopped, wo.Status);
    }

    [Fact]
    public void WO_FullProduction_AutoCompletes()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        Assert.Equal(100m, wo.ProducedQuantity);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void WO_Overproduction_Blocked()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        Assert.Throws<BusinessException>(() => wo.RecordProduction(1)); // Already at 100%
    }

    [Fact]
    public void WO_Overproduction_WithAllowance()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        // 10% overproduction allowed
        wo.RecordProduction(105, overproductionPercentage: 10m);
        Assert.Equal(105m, wo.ProducedQuantity);
    }

    // ============================
    // Material Request Lifecycle
    // ============================

    [Fact]
    public void MR_FullLifecycle()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), CompanyId, "MR-001", MaterialRequestType.Purchase, DateTime.Today);
        mr.AddItem(ItemId, "Raw Material", 100, "Unit");
        Assert.Equal(DocumentStatus.Draft, mr.Status);

        mr.Submit();
        Assert.Equal(DocumentStatus.Submitted, mr.Status);

        mr.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, mr.Status);
    }

    [Fact]
    public void MR_CannotSubmitEmpty()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), CompanyId, "MR-002", MaterialRequestType.Purchase, DateTime.Today);
        Assert.Throws<BusinessException>(() => mr.Submit());
    }

    // ============================
    // Quotation Lifecycle
    // ============================

    [Fact]
    public void Quotation_Lifecycle_SubmitCancelAmend()
    {
        var q = new Quotation(Guid.NewGuid(), CompanyId, CustomerId, "QTN-001", DateTime.Today);
        q.AddItem(ItemId, "Widget", 5, 200, 0);
        Assert.Equal(DocumentStatus.Draft, q.Status);

        q.Submit();
        Assert.Equal(DocumentStatus.Submitted, q.Status);

        q.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, q.Status);
    }

    [Fact]
    public void Quotation_Expiry()
    {
        var q = new Quotation(Guid.NewGuid(), CompanyId, CustomerId, "QTN-002", DateTime.Today);
        q.AddItem(ItemId, "Widget", 1, 100, 0);
        q.ValidUntil = DateTime.Today.AddDays(-1);
        q.Submit();
        Assert.True(q.IsExpired);
    }

    [Fact]
    public void Quotation_NotExpiredWhenFuture()
    {
        var q = new Quotation(Guid.NewGuid(), CompanyId, CustomerId, "QTN-003", DateTime.Today);
        q.AddItem(ItemId, "Widget", 1, 100, 0);
        q.ValidUntil = DateTime.Today.AddDays(30);
        q.Submit();
        Assert.False(q.IsExpired);
    }

    [Fact]
    public void Quotation_MarkLost()
    {
        var q = new Quotation(Guid.NewGuid(), CompanyId, CustomerId, "QTN-004", DateTime.Today);
        q.AddItem(ItemId, "Widget", 1, 100, 0);
        q.Submit();
        q.MarkLost();
        Assert.Equal(DocumentStatus.Rejected, q.Status);
    }

    // ============================
    // Amendment Verification
    // ============================

    [Fact]
    public void AllAmendableDocuments_HaveAmendmentFields()
    {
        // SI
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-A1", DateTime.Today);
        si.AmendedFromId = Guid.NewGuid();
        si.AmendmentIndex = 1;
        Assert.Equal(1, si.AmendmentIndex);

        // PI
        var pi = new PurchaseInvoice(Guid.NewGuid(), CompanyId, SupplierId, "PI-A1", DateTime.Today);
        pi.AmendedFromId = Guid.NewGuid();
        pi.AmendmentIndex = 2;
        Assert.Equal(2, pi.AmendmentIndex);

        // SO
        var so = new SalesOrder(Guid.NewGuid(), CompanyId, CustomerId, "SO-A1", DateTime.Today);
        so.AmendedFromId = Guid.NewGuid();
        Assert.NotNull(so.AmendedFromId);

        // PO
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-A1", DateTime.Today);
        po.AmendedFromId = Guid.NewGuid();
        Assert.NotNull(po.AmendedFromId);
    }

    // ============================
    // Edge Cases: Double Transitions
    // ============================

    [Fact]
    public void SI_DoubleSubmit_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-DS", DateTime.Today);
        si.AddItem(ItemId, "Widget", 1, 100, 0);
        si.Submit();
        Assert.Throws<BusinessException>(() => si.Submit());
    }

    [Fact]
    public void SI_DoubleCancel_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), CompanyId, CustomerId, "SI-DC", DateTime.Today);
        si.AddItem(ItemId, "Widget", 1, 100, 0);
        si.Submit();
        si.Post();
        si.Cancel();
        Assert.Throws<BusinessException>(() => si.Cancel());
    }

    [Fact]
    public void PO_DoubleSubmit_Throws()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, "PO-DS", DateTime.Today);
        po.AddItem(ItemId, "Material", 10, 50, 0);
        po.Submit();
        Assert.Throws<BusinessException>(() => po.Submit());
    }

    [Fact]
    public void WO_DoubleStart_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        Assert.Throws<BusinessException>(() => wo.Start());
    }

    [Fact]
    public void WO_Cancel_From_Stopped_Throws()
    {
        // Per DO-NOT: "Cancel Stopped Work Order directly (must Unstop first, then cancel)"
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        Assert.Equal(WorkOrderStatus.Stopped, wo.Status);
        Assert.Throws<BusinessException>(() => wo.Cancel());
    }

    [Fact]
    public void WO_Unstop_From_Stopped_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Unstop();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WO_Unstop_From_NonStopped_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        Assert.Throws<BusinessException>(() => wo.Unstop());
    }

    [Fact]
    public void WO_Cancel_From_Submitted_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WO_Cancel_From_InProcess_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WO_Unstop_Then_Cancel_Succeeds()
    {
        // Correct workflow: Stop → Unstop → Cancel
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Unstop();
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WO_Cancel_From_Completed_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.Throws<BusinessException>(() => wo.Cancel());
    }
}

