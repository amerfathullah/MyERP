using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Comprehensive end-to-end tests verifying the complete business flow
/// works correctly at the domain entity level, including all guards and validations.
/// </summary>
public class CompleteBusinessFlowTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void FullSalesCycle_SO_DN_SI_CorrectStatusProgression()
    {
        // Create SO with 2 items
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-E2E-001", DateTime.UtcNow, _tenantId);
        so.AddItem(Guid.NewGuid(), "Widget A", 100, 50, 0, "Unit");
        so.AddItem(Guid.NewGuid(), "Widget B", 50, 100, 0, "Unit");
        so.Status.ShouldBe(DocumentStatus.Draft);

        // Submit → ToDeliverAndBill
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        so.GrandTotal.ShouldBe(10000m); // (100×50) + (50×100)

        // Partial delivery of item A (60 of 100)
        so.Items[0].DeliveredQty = 60;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // Min(60%, 0%) < 100
        so.PerDelivered.ShouldBe(0m); // Min(60/100=60%, 0/50=0%) = 0%

        // Full delivery of item B (50 of 50)
        so.Items[1].DeliveredQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // Min(60%, 100%) = 60% < 100
        so.PerDelivered.ShouldBe(60m);

        // Complete delivery of item A
        so.Items[0].DeliveredQty = 100;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill); // Min(100%, 100%) = 100%, but 0% billed

        // Full billing
        so.Items[0].BilledQty = 100;
        so.Items[1].BilledQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void PurchaseCycle_PO_PR_PI_CorrectStatusProgression()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-E2E-001", DateTime.UtcNow, _tenantId);
        po.AddItem(Guid.NewGuid(), "Raw Material", 200, 25, 0, "Kg");
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Partial receipt
        po.Items[0].ReceivedQty = 120;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // 60% received, 0% billed

        // Full receipt
        po.Items[0].ReceivedQty = 200;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToBill); // 100% received, 0% billed

        // Full billing
        po.Items[0].BilledQty = 200;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void CreditNote_ReducesOriginalOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SI-E2E-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Service", 1, 5000, 0);
        si.Submit();
        si.Post();
        si.OutstandingAmount.ShouldBe(5000m);

        // Simulate credit note reducing outstanding
        si.AmountPaid += 2000m; // Credit note for 2000
        si.OutstandingAmount.ShouldBe(3000m);
    }

    [Fact]
    public void PaymentEntry_ExchangeGainLoss_Calculation()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive,
            DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid(), _tenantId);

        pe.ExchangeRate = 4.5m;        // Payment at MYR 4.50/USD
        pe.SourceExchangeRate = 4.3m;   // Invoice was at MYR 4.30/USD

        // Gain: paid at higher rate → favorable for receivable
        pe.ExchangeGainLoss.ShouldBe(200m); // 1000 × (4.5 - 4.3) = 200
        pe.BaseAmount.ShouldBe(4500m);      // 1000 × 4.5
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_WithPartialAllocation()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive,
            DateTime.UtcNow, 20000m, Guid.NewGuid(), Guid.NewGuid());

        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 15000m, 12000m, 12000m));
        pe.References.Add(new PaymentEntryReference(Guid.NewGuid(), pe.Id,
            "SalesInvoice", Guid.NewGuid(), 10000m, 5000m, 5000m));

        // 20000 - (12000 + 5000) = 3000 unallocated
        pe.UnallocatedAmount.ShouldBe(3000m);
    }

    [Fact]
    public void WorkOrder_ProductionOverproduction_Blocked()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-E2E-001",
            Guid.NewGuid(), Guid.NewGuid(), 100, _tenantId);
        wo.SetPlannedDates(DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        wo.Submit();
        wo.Start();

        // Produce within limit but try to exceed in single call (5% overproduction allowed, max=105)
        wo.RecordProduction(80, 5);
        wo.ProducedQuantity.ShouldBe(80);

        // Try to exceed max (80 + 30 = 110 > 105)
        Should.Throw<BusinessException>(() => wo.RecordProduction(30, 5))
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderOverproduction);
    }

    [Fact]
    public void BOM_PhantomAndSubAssembly_Configuration()
    {
        var parentBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-PARENT", Guid.NewGuid());

        // Raw material (no sub-BOM)
        var rm = new BomItem(Guid.NewGuid(), parentBom.Id, Guid.NewGuid(), "Steel", 10, 25);
        parentBom.Items.Add(rm);

        // Sub-assembly with BOM (produced independently)
        var subAsm = new BomItem(Guid.NewGuid(), parentBom.Id, Guid.NewGuid(), "Frame", 2, 150);
        subAsm.SubBomId = Guid.NewGuid();
        subAsm.IsPhantom = false;
        parentBom.Items.Add(subAsm);

        // Phantom item (components bubble up)
        var phantom = new BomItem(Guid.NewGuid(), parentBom.Id, Guid.NewGuid(), "Hardware Kit", 1, 50);
        phantom.SubBomId = Guid.NewGuid();
        phantom.IsPhantom = true;
        parentBom.Items.Add(phantom);

        parentBom.RecalculateCost();
        // (10×25) + (2×150) + (1×50) = 250 + 300 + 50 = 600
        parentBom.TotalMaterialCost.ShouldBe(600m);

        parentBom.Items.Count(i => i.IsPhantom).ShouldBe(1);
        parentBom.Items.Count(i => i.SubBomId.HasValue).ShouldBe(2);
    }

    [Fact]
    public void Company_CurrencyLock_PreventsChangeWithTransactions()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp", _tenantId);
        company.SetCurrency("MYR", hasSubmittedTransactions: false);
        company.CurrencyCode.ShouldBe("MYR");

        // After transactions exist, cannot change
        Should.Throw<BusinessException>(() => company.SetCurrency("USD", hasSubmittedTransactions: true))
            .Code.ShouldBe(MyERPDomainErrorCodes.CompanyCurrencyLocked);

        // Same currency always OK
        company.SetCurrency("MYR", hasSubmittedTransactions: true);
        company.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void SOClose_StopsDelivery_Reopen_Resumes()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-CLOSE-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item", 100, 50, 0);
        so.Submit();

        // Partial delivery
        so.Items[0].DeliveredQty = 60;
        so.UpdateFulfillmentStatus();

        // Close (short-close)
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);

        // Reopen → recalculates status
        so.Reopen();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // 60% delivered, 0% billed
    }

    [Fact]
    public void BlanketOrder_AllowanceEnforcement()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), _companyId, "BO-001", "Selling",
            Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        bo.AddItem(Guid.NewGuid(), 1000, 10, "Material");
        bo.Submit();

        // Order 900 (within limit)
        bo.Items[0].RecordOrder(900, 10); // max = 1000 × 1.10 = 1100
        bo.Items[0].OrderedQty.ShouldBe(900);

        // Order 200 more (total 1100 = exactly at 10% allowance)
        bo.Items[0].RecordOrder(200, 10); // 900 + 200 = 1100 = max
        bo.Items[0].OrderedQty.ShouldBe(1100);

        // Exceed allowance
        Should.Throw<BusinessException>(() => bo.Items[0].RecordOrder(1, 10)); // 1101 > 1100
    }

    [Fact]
    public void Bin_ProjectedQty_Formula()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100;
        bin.OrderedQty = 50;
        bin.IndentedQty = 20;
        bin.PlannedQty = 30;
        bin.ReservedQty = 40;
        bin.ReservedQtyForProduction = 10;
        bin.ReservedQtyForSubContract = 5;
        bin.ReservedQtyForProductionPlan = 3;

        // Projected = 100 + 50 + 20 + 30 - 40 - 10 - 5 - 3 = 142
        bin.ProjectedQty.ShouldBe(142m);
    }
}
