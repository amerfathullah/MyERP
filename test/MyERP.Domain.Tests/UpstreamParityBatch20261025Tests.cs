using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity sync (ERPNext PRs #62244, #61667, #62249, #62218, #61468, #61493, #62103).
/// </summary>
public class UpstreamParityBatch20261025Tests
{
    [Fact]
    public void Dunning_AddOverduePayment_ThrowsWhenNonPositive()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1);
        
        var ex = Assert.Throws<BusinessException>(() =>
            dunning.AddOverduePayment(Guid.NewGuid(), 0m, DateTime.UtcNow.AddDays(-10), 10));
        
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void PaymentOrder_AddReference_EnforcesMatchingReferenceType()
    {
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();

        // 1. PaymentRequest order accepts PaymentRequest, rejects PaymentEntry
        var prOrder = new PaymentOrder(Guid.NewGuid(), companyId, PaymentOrderType.PaymentRequest,
            DateTime.UtcNow, bankAccountId);

        var ref1 = prOrder.AddReference("PaymentRequest", Guid.NewGuid(), 100m, Guid.NewGuid(), "Bank Transfer", bankAccountId);
        Assert.NotNull(ref1);
        Assert.Single(prOrder.References);

        var ex1 = Assert.Throws<BusinessException>(() =>
            prOrder.AddReference("PaymentEntry", Guid.NewGuid(), 50m, Guid.NewGuid(), "Bank Transfer", bankAccountId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex1.Code);

        // 2. PaymentEntry order accepts PaymentEntry, rejects PaymentRequest
        var peOrder = new PaymentOrder(Guid.NewGuid(), companyId, PaymentOrderType.PaymentEntry,
            DateTime.UtcNow, bankAccountId);

        var ref2 = peOrder.AddReference("PaymentEntry", Guid.NewGuid(), 200m, Guid.NewGuid(), "Bank Transfer", bankAccountId);
        Assert.NotNull(ref2);
        Assert.Single(peOrder.References);

        var ex2 = Assert.Throws<BusinessException>(() =>
            peOrder.AddReference("PaymentRequest", Guid.NewGuid(), 75m, Guid.NewGuid(), "Bank Transfer", bankAccountId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex2.Code);
    }

    [Fact]
    public void MaterialRequest_ClearSourceWarehouseIfNotTransfer_ClearsWarehouseWhenNotTransfer()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-0001",
            MaterialRequestType.Purchase, DateTime.UtcNow)
        {
            SourceWarehouseId = Guid.NewGuid(),
            TargetWarehouseId = Guid.NewGuid()
        };

        mr.ClearSourceWarehouseIfNotTransfer();
        Assert.Null(mr.SourceWarehouseId);
        Assert.NotNull(mr.TargetWarehouseId);

        // When MaterialTransfer, keeps SourceWarehouseId
        var mrTransfer = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-0002",
            MaterialRequestType.MaterialTransfer, DateTime.UtcNow)
        {
            SourceWarehouseId = Guid.NewGuid(),
            TargetWarehouseId = Guid.NewGuid()
        };

        var sourceWh = mrTransfer.SourceWarehouseId;
        mrTransfer.ClearSourceWarehouseIfNotTransfer();
        Assert.Equal(sourceWh, mrTransfer.SourceWarehouseId);
    }

    [Fact]
    public void Subscription_GracePeriod_EvaluatedFromDueDateStrictlyAfter()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            startDate, "Monthly")
        {
            CurrentInvoiceStart = startDate,
            CurrentInvoiceEnd = startDate.AddDays(30), // Day 30
            DaysUntilDue = 5,                         // Due on Day 35
            CancelAfterGraceDays = 3                  // Grace period ends on Day 38
        };

        var invoiceEnd = sub.CurrentInvoiceEnd.Value;
        var dueDate = invoiceEnd.AddDays(sub.DaysUntilDue);
        var graceEnd = dueDate.AddDays(sub.CancelAfterGraceDays);

        // Day 34: before due date -> Active
        Assert.Equal(SubscriptionStatus.Active, engine.DetermineStatus(sub, dueDate.AddDays(-1), hasOutstandingInvoices: true, isFullyRefunded: false));

        // Day 35: on due date -> Active (due date has not passed yet, strictly >)
        Assert.Equal(SubscriptionStatus.Active, engine.DetermineStatus(sub, dueDate, hasOutstandingInvoices: true, isFullyRefunded: false));

        // Day 36: 1 day past due -> GracePeriod
        Assert.Equal(SubscriptionStatus.GracePeriod, engine.DetermineStatus(sub, dueDate.AddDays(1), hasOutstandingInvoices: true, isFullyRefunded: false));

        // Day 38: on grace period end -> GracePeriod (strictly > grace period end to cancel)
        Assert.Equal(SubscriptionStatus.GracePeriod, engine.DetermineStatus(sub, graceEnd, hasOutstandingInvoices: true, isFullyRefunded: false));

        // Day 39: 1 day past grace period end -> Cancelled
        Assert.Equal(SubscriptionStatus.Cancelled, engine.DetermineStatus(sub, graceEnd.AddDays(1), hasOutstandingInvoices: true, isFullyRefunded: false));
    }

    [Fact]
    public void DeliveryScheduleService_SyncScheduleWithItem_RescalesAndRemovesExcess()
    {
        var service = new DeliveryScheduleService();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var entries = new List<DeliveryScheduleEntry>
        {
            new DeliveryScheduleEntry(Guid.NewGuid(), orderId, itemId, new DateTime(2026, 1, 1), 5m),
            new DeliveryScheduleEntry(Guid.NewGuid(), orderId, itemId, new DateTime(2026, 2, 1), 5m),
            new DeliveryScheduleEntry(Guid.NewGuid(), orderId, itemId, new DateTime(2026, 3, 1), 5m)
        };

        // Rescale from 15m to 8m: first keeps 5m, second takes 3m, third is removed (excess)
        var synced = service.SyncScheduleWithItem(entries, 8m);

        Assert.Equal(2, synced.Count);
        Assert.Equal(5m, synced[0].ScheduledQty);
        Assert.Equal(3m, synced[1].ScheduledQty);

        // Rescale to 0m: all removed
        var zeroSynced = service.SyncScheduleWithItem(entries, 0m);
        Assert.Empty(zeroSynced);
    }

    [Fact]
    public void RepostItemValuation_ValidateRecalculateValuationRate_EnforcesScope()
    {
        var companyId = Guid.NewGuid();
        var postingDate = DateTime.UtcNow;

        // 1. Rejects non-transaction based
        var rivNonTx = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse, postingDate)
        {
            RecalculateValuationRate = true
        };
        var ex1 = Assert.Throws<BusinessException>(() => rivNonTx.ValidateRecalculateValuationRate());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex1.Code);

        // 2. Transaction based on Purchase Invoice requires Update Stock
        var rivPiNoStock = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.Transaction, postingDate)
        {
            RecalculateValuationRate = true,
            VoucherType = "Purchase Invoice",
            VoucherId = Guid.NewGuid()
        };
        var ex2 = Assert.Throws<BusinessException>(() => rivPiNoStock.ValidateRecalculateValuationRate(isUpdateStock: false));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex2.Code);

        // 3. Transaction based on Purchase Invoice with Update Stock allowed
        rivPiNoStock.ValidateRecalculateValuationRate(isUpdateStock: true);

        // 4. Transaction based on Stock Entry rejects material transfers
        var rivSeTransfer = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.Transaction, postingDate)
        {
            RecalculateValuationRate = true,
            VoucherType = "Stock Entry",
            VoucherId = Guid.NewGuid()
        };
        var ex3 = Assert.Throws<BusinessException>(() => rivSeTransfer.ValidateRecalculateValuationRate(isTransferStockEntry: true));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex3.Code);

        // 5. Transaction based on non-transfer Stock Entry allowed
        rivSeTransfer.ValidateRecalculateValuationRate(isTransferStockEntry: false);

        // 6. Transaction based on Purchase Receipt allowed
        var rivPr = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.Transaction, postingDate)
        {
            RecalculateValuationRate = true,
            VoucherType = "Purchase Receipt",
            VoucherId = Guid.NewGuid()
        };
        rivPr.ValidateRecalculateValuationRate();
    }

    [Fact]
    public void PickListManager_FilterLocationsByPickedMaterials_RoundsRemainingQty()
    {
        var whId = Guid.NewGuid();
        var locations = new List<PickLocationAllocation>
        {
            new PickLocationAllocation { WarehouseId = whId, Qty = 10.333333m }
        };
        var pickedMap = new Dictionary<(Guid WarehouseId, Guid? BatchId), decimal>
        {
            { (whId, null), 2.111111m }
        };

        var filtered = PickListManager.FilterLocationsByPickedMaterials(locations, pickedMap);

        Assert.Single(filtered);
        Assert.Equal(8.2222m, filtered[0].Qty);
    }

    [Fact]
    public void JobCardManager_GetOperationTargetQty_ScalesForSemiFinishedGoods()
    {
        var companyId = Guid.NewGuid();
        var mainItemId = Guid.NewGuid();
        var semiFgItemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var bom = new BillOfMaterials(bomId, companyId, "BOM-001", mainItemId)
        {
            Quantity = 2m
        };
        // BomItem: 5 units of semiFg for 2 units of mainItem (2.5 per unit)
        var bomItem = new BomItem(Guid.NewGuid(), bomId, semiFgItemId, "SemiFG", 5m, 10m);
        bom.AddItem(bomItem);

        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", mainItemId, bomId, 10m)
        {
            TrackSemiFinishedGoods = true
        };

        var opSemiFg = new BomOperation(Guid.NewGuid(), bomId, opId, 1, 30m)
        {
            FinishedGoodItemId = semiFgItemId,
            IsFinalFinishedGood = false
        };

        var targetQty = JobCardManager.GetOperationTargetQty(wo, opSemiFg, bom);
        // 10m * (5m / 2m) = 25m
        Assert.Equal(25m, targetQty);

        // When IsFinalFinishedGood is true, returns wo.Quantity (10m)
        opSemiFg.IsFinalFinishedGood = true;
        Assert.Equal(10m, JobCardManager.GetOperationTargetQty(wo, opSemiFg, bom));

        // When TrackSemiFinishedGoods is false, returns wo.Quantity (10m)
        opSemiFg.IsFinalFinishedGood = false;
        wo.TrackSemiFinishedGoods = false;
        Assert.Equal(10m, JobCardManager.GetOperationTargetQty(wo, opSemiFg, bom));
    }
}
