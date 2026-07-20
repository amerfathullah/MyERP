using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.FinancialIntegration;

public class FinancialFlowTests
{
    private static readonly Guid Co = Guid.NewGuid();
    private static readonly Guid Cust = Guid.NewGuid();
    private static readonly Guid Supp = Guid.NewGuid();
    private static readonly Guid FY = Guid.NewGuid();

    // ========== GL Posting ==========

    [Fact]
    public void AccountingRule_Properties()
    {
        var r = new AccountingRule(Guid.NewGuid(), Co, "SI-DR", "SalesInvoice",
            true, AccountSource.CustomerReceivable, AmountSource.GrandTotal);
        r.DocumentType.ShouldBe("SalesInvoice");
        r.IsDebit.ShouldBeTrue();
        r.AccountSource.ShouldBe(AccountSource.CustomerReceivable);
    }

    [Fact]
    public void AccountingRule_InactiveFiltered()
    {
        var a = new AccountingRule(Guid.NewGuid(), Co, "A", "SI", true,
            AccountSource.FixedAccount, AmountSource.GrandTotal) { IsActive = true };
        var b = new AccountingRule(Guid.NewGuid(), Co, "B", "SI", false,
            AccountSource.FixedAccount, AmountSource.GrandTotal) { IsActive = false };
        new[] { a, b }.Count(r => r.IsActive).ShouldBe(1);
    }

    [Fact]
    public void JE_Balanced_Posts()
    {
        var je = new JournalEntry(Guid.NewGuid(), Co, FY, DateTime.Today);
        je.AddLine(Guid.NewGuid(), 1060m, true, "Receivable");
        je.AddLine(Guid.NewGuid(), 1000m, false, "Revenue");
        je.AddLine(Guid.NewGuid(), 60m, false, "SST");
        je.TotalDebit.ShouldBe(1060m);
        je.TotalCredit.ShouldBe(1060m);
        Should.NotThrow(() => je.Post());
        je.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void JE_Unbalanced_Throws()
    {
        var je = new JournalEntry(Guid.NewGuid(), Co, FY, DateTime.Today);
        je.AddLine(Guid.NewGuid(), 1000m, true);
        je.AddLine(Guid.NewGuid(), 999m, false);
        Should.Throw<BusinessException>(() => je.Post());
    }

    [Fact]
    public void JE_Empty_Throws()
    {
        var je = new JournalEntry(Guid.NewGuid(), Co, FY, DateTime.Today);
        Should.Throw<BusinessException>(() => je.Post());
    }

    // ========== Payment Ledger Entry ==========

    [Fact]
    public void PLE_Positive_Outstanding()
    {
        var ple = new PaymentLedgerEntry(Guid.NewGuid(), Co, DateTime.Today,
            Guid.NewGuid(), "Customer", Cust, "SalesInvoice", Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), 5000m, 5000m, "MYR");
        ple.Amount.ShouldBe(5000m);
        ple.IsReversal.ShouldBeFalse();
    }

    [Fact]
    public void PLE_Payment_Reduces()
    {
        var inv = Guid.NewGuid();
        var si = new PaymentLedgerEntry(Guid.NewGuid(), Co, DateTime.Today,
            Guid.NewGuid(), "Customer", Cust, "SalesInvoice", inv,
            "SalesInvoice", inv, 5000m, 5000m, "MYR");
        var pe = new PaymentLedgerEntry(Guid.NewGuid(), Co, DateTime.Today,
            Guid.NewGuid(), "Customer", Cust, "PaymentEntry", Guid.NewGuid(),
            "SalesInvoice", inv, -3000m, -3000m, "MYR");
        (si.Amount + pe.Amount).ShouldBe(2000m);
    }

    [Fact]
    public void PLE_Delinked_Excluded()
    {
        var ple = new PaymentLedgerEntry(Guid.NewGuid(), Co, DateTime.Today,
            Guid.NewGuid(), "Customer", Cust, "PaymentEntry", Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), -2000m, -2000m, "MYR");
        ple.Delinked = true;
        new[] { ple }.Where(e => !e.Delinked && !e.IsReversal).Sum(e => e.Amount).ShouldBe(0m);
    }

    // ========== Payment Entry ==========

    [Fact]
    public void PE_MultiRef_FullyAllocated()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Co, PaymentType.Receive,
            DateTime.Today, 8000m, Guid.NewGuid(), Guid.NewGuid());
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 6000m, 6000m, 5000m));
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 4000m, 4000m, 3000m));
        pe.UnallocatedAmount.ShouldBe(0m);
    }

    [Fact]
    public void PE_PartialAlloc_HasUnallocated()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Co, PaymentType.Receive,
            DateTime.Today, 10000m, Guid.NewGuid(), Guid.NewGuid());
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 8000m, 8000m, 6000m));
        pe.UnallocatedAmount.ShouldBe(4000m);
    }

    [Fact]
    public void PE_ExchangeGain()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Co, PaymentType.Receive,
            DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid())
        { ExchangeRate = 4.80m, SourceExchangeRate = 4.72m };
        pe.BaseAmount.ShouldBe(4800m);
        pe.ExchangeGainLoss.ShouldBe(80m);
    }

    [Fact]
    public void PE_ExchangeLoss()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Co, PaymentType.Receive,
            DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid())
        { ExchangeRate = 4.60m, SourceExchangeRate = 4.72m };
        pe.ExchangeGainLoss.ShouldBe(-120m);
    }

    // ========== Sales Invoice ==========

    [Fact]
    public void SI_Outstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Co, Cust, "SI-001", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m, "Unit");
        si.GrandTotal.ShouldBe(1000m);
        si.AmountPaid = 400m;
        si.OutstandingAmount.ShouldBe(600m);
    }

    [Fact]
    public void SI_CreditNote_Negative()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Co, Cust, "CN-001", DateTime.Today)
        { IsReturn = true, ReturnAgainstId = Guid.NewGuid() };
        si.AddItem(Guid.NewGuid(), "Return", -5, 100m, 0m, "Unit");
        si.GrandTotal.ShouldBe(-500m);
    }

    [Fact]
    public void SI_MultiCurrency()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Co, Cust, "SI-USD", DateTime.Today)
        { ExchangeRate = 4.72m };
        si.AddItem(Guid.NewGuid(), "Service", 1, 1000m, 0m, "Unit");
        si.BaseGrandTotal.ShouldBe(4720m);
    }

    // ========== Purchase Invoice ==========

    [Fact]
    public void PI_FullPayment_Zero()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Co, Supp, "PI-001", DateTime.Today);
        pi.AddItem(Guid.NewGuid(), "Material", 20, 50m, 0m, "kg");
        pi.AmountPaid = 1000m;
        pi.OutstandingAmount.ShouldBe(0m);
    }

    // ========== Order Fulfillment ==========

    [Fact]
    public void SO_FullFulfillment_Completes()
    {
        var so = new SalesOrder(Guid.NewGuid(), Co, Cust, "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "A", 10, 100m, 0m, "Unit");
        so.AddItem(Guid.NewGuid(), "B", 5, 200m, 0m, "Unit");
        so.Submit();
        so.Items[0].DeliveredQty = 10; so.Items[0].BilledQty = 10;
        so.Items[1].DeliveredQty = 5; so.Items[1].BilledQty = 5;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void SO_Partial_StaysOpen()
    {
        var so = new SalesOrder(Guid.NewGuid(), Co, Cust, "SO-002", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "X", 10, 50m, 0m, "Unit");
        so.Submit();
        so.Items[0].DeliveredQty = 5;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void SO_CloseReopen()
    {
        var so = new SalesOrder(Guid.NewGuid(), Co, Cust, "SO-003", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "I", 100, 10m, 0m, "Unit");
        so.Submit();
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);
        so.Reopen();
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void PO_FullReceipt_ToBill()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Co, Supp, "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Mat", 50, 20m, 0m, "kg");
        po.Submit();
        po.Items[0].ReceivedQty = 50;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToBill);
    }

    // ========== FIFO ==========

    [Fact]
    public void FIFO_OldestFirst()
    {
        var f = new FifoValuation();
        f.AddStock(10m, 100m);
        f.AddStock(5m, 120m);
        var consumed = f.RemoveStock(12m);
        consumed.Sum(b => b.Qty).ShouldBe(12m);
        f.TotalQty.ShouldBe(3m);
    }

    [Fact]
    public void FIFO_Exact_Empties()
    {
        var f = new FifoValuation();
        f.AddStock(5m, 50m);
        f.RemoveStock(5m);
        f.TotalQty.ShouldBe(0m);
    }

    // ========== Payment Schedule ==========

    [Fact]
    public void Schedule_RecordPayment()
    {
        var e = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today.AddDays(30), 50m, 5000m);
        e.Outstanding.ShouldBe(5000m);
        e.RecordPayment(3000m).ShouldBe(3000m);
        e.Outstanding.ShouldBe(2000m);
    }

    [Fact]
    public void Schedule_OverpayCapped()
    {
        var e = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            DateTime.Today.AddDays(30), 100m, 1000m);
        e.RecordPayment(1500m).ShouldBe(1000m);
        e.IsFullyPaid.ShouldBeTrue();
    }

    // ========== Payment Terms ==========

    [Fact]
    public void Terms_Net30()
    {
        var t = new PaymentTermsTemplate(Guid.NewGuid(), "Net 30");
        t.AddTerm(new PaymentTerm(Guid.NewGuid(), t.Id, 100m, 30, "Net 30"));
        var s = t.GenerateSchedule(new DateTime(2026, 7, 1), 10000m);
        s.Count.ShouldBe(1);
        s[0].DueDate.ShouldBe(new DateTime(2026, 7, 31));
    }

    [Fact]
    public void Terms_InvalidPortions_Throws()
    {
        var t = new PaymentTermsTemplate(Guid.NewGuid(), "Bad");
        t.AddTerm(new PaymentTerm(Guid.NewGuid(), t.Id, 60m, 0));
        t.AddTerm(new PaymentTerm(Guid.NewGuid(), t.Id, 60m, 30));
        Should.Throw<BusinessException>(() => t.ValidatePortions());
    }

    // ========== Bin ==========

    [Fact]
    public void Bin_ProjectedQty()
    {
        var b = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        { ActualQty = 100, ReservedQty = 20, OrderedQty = 50, IndentedQty = 10,
          PlannedQty = 30, ReservedQtyForProduction = 15,
          ReservedQtyForSubContract = 5, ReservedQtyForProductionPlan = 10 };
        b.ProjectedQty.ShouldBe(140m);
    }

    [Fact]
    public void CostCenter_GroupVsLeaf()
    {
        var root = new CostCenter(Guid.NewGuid(), Co, "HQ", isGroup: true);
        root.IsGroup.ShouldBeTrue();
        var leaf = new CostCenter(Guid.NewGuid(), Co, "Sales", parentId: root.Id);
        leaf.IsGroup.ShouldBeFalse();
    }
}
