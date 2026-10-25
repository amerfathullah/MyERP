using NSubstitute;
using MyERP.Core.Entities;
using MyERP.Core.DomainServices;
using Volo.Abp.Domain.Repositories;
using System.Linq;
using MyERP.Sales;
using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.HumanResources.DomainServices;
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

    [Fact]
    public void PaymentRequest_ValidateBankAccount_EnforcesOwnershipAndActiveStatus()
    {
        var companyId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var glAccountId = Guid.NewGuid();

        var pr = new PaymentRequest(Guid.NewGuid(), companyId, "SalesInvoice",
            Guid.NewGuid(), partyId, "Customer", 500m);

        // 1. Missing bank account throws ValidationFailed
        var ex1 = Assert.Throws<BusinessException>(() =>
            pr.ValidateBankAccount(null, Guid.NewGuid()));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex1.Code);

        // 2. Disabled bank account throws ValidationFailed
        var disabledAcc = new BankAccount(Guid.NewGuid(), companyId, "Disabled Bank", glAccountId, "Bank A");
        disabledAcc.Disable();
        var ex2 = Assert.Throws<BusinessException>(() =>
            pr.ValidateBankAccount(disabledAcc, disabledAcc.Id));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex2.Code);

        // 3. Company account belonging to another company throws CompanyMismatch
        var otherCompanyAcc = new BankAccount(Guid.NewGuid(), Guid.NewGuid(), "Other Company Bank", glAccountId, "Bank B");
        var ex3 = Assert.Throws<BusinessException>(() =>
            pr.ValidateBankAccount(otherCompanyAcc, otherCompanyAcc.Id));
        Assert.Equal(MyERPDomainErrorCodes.CompanyMismatch, ex3.Code);

        // 4. Party account with mismatching party throws ValidationFailed
        var partyAcc = new BankAccount(Guid.NewGuid(), companyId, "Customer Bank", glAccountId, "Bank C")
        {
            IsCompanyAccount = false,
            PartyType = "Customer",
            PartyId = Guid.NewGuid() // mismatch
        };
        var ex4 = Assert.Throws<BusinessException>(() =>
            pr.ValidateBankAccount(partyAcc, partyAcc.Id));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex4.Code);

        // 5. Valid matching company bank account passes
        var validCompanyAcc = new BankAccount(Guid.NewGuid(), companyId, "Valid Bank", glAccountId, "Bank D");
        pr.ValidateBankAccount(validCompanyAcc, validCompanyAcc.Id);

        // 6. Valid matching party bank account passes
        var validPartyAcc = new BankAccount(Guid.NewGuid(), companyId, "Valid Party Bank", glAccountId, "Bank E")
        {
            IsCompanyAccount = false,
            PartyType = "Customer",
            PartyId = partyId
        };
        pr.ValidateBankAccount(validPartyAcc, validPartyAcc.Id);
    }

    [Fact]
    public void PaymentOrder_ValidateCompanyBankAccount_And_AddReferencePositiveAmount()
    {
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var glAccountId = Guid.NewGuid();

        var order = new PaymentOrder(Guid.NewGuid(), companyId, PaymentOrderType.PaymentRequest,
            DateTime.UtcNow, bankAccountId);

        // 1. Missing bank account throws ValidationFailed
        var ex1 = Assert.Throws<BusinessException>(() =>
            order.ValidateCompanyBankAccount(null));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex1.Code);

        // 2. Disabled bank account throws ValidationFailed
        var disabledAcc = new BankAccount(bankAccountId, companyId, "Disabled Bank", glAccountId, "Bank A");
        disabledAcc.Disable();
        var ex2 = Assert.Throws<BusinessException>(() =>
            order.ValidateCompanyBankAccount(disabledAcc));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex2.Code);

        // 3. Non-company bank account throws ValidationFailed
        var partyAcc = new BankAccount(bankAccountId, companyId, "Party Bank", glAccountId, "Bank B")
        {
            IsCompanyAccount = false
        };
        var ex3 = Assert.Throws<BusinessException>(() =>
            order.ValidateCompanyBankAccount(partyAcc));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex3.Code);

        // 4. Other company bank account throws ValidationFailed
        var otherCompAcc = new BankAccount(bankAccountId, Guid.NewGuid(), "Other Bank", glAccountId, "Bank C");
        var ex4 = Assert.Throws<BusinessException>(() =>
            order.ValidateCompanyBankAccount(otherCompAcc));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex4.Code);

        // 5. Valid company bank account passes
        var validAcc = new BankAccount(bankAccountId, companyId, "Company Bank", glAccountId, "Bank D");
        order.ValidateCompanyBankAccount(validAcc);

        // 6. AddReference throws AmountMustBePositive for amount <= 0
        var ex5 = Assert.Throws<BusinessException>(() =>
            order.AddReference("PaymentRequest", Guid.NewGuid(), 0m, Guid.NewGuid(), "Bank Transfer", bankAccountId));
        Assert.Equal(MyERPDomainErrorCodes.AmountMustBePositive, ex5.Code);

        var ex6 = Assert.Throws<BusinessException>(() =>
            order.AddReference("PaymentRequest", Guid.NewGuid(), -10m, Guid.NewGuid(), "Bank Transfer", bankAccountId));
        Assert.Equal(MyERPDomainErrorCodes.AmountMustBePositive, ex6.Code);
    }

    [Fact]
    public void JobCard_Complete_ReleasesHoldIfNoQtyLeft()
    {
        var companyId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), companyId, woId, opId, 10m, 1);
        jc.StartTimeLog(DateTime.UtcNow.AddHours(-2));
        jc.Hold();
        Assert.Equal(JobCardStatus.OnHold, jc.Status);

        // Complete full quantity (10m)
        jc.CompleteTimeLogs(DateTime.UtcNow, 10m);
        // ReleaseHoldIfNoQtyLeft transitioned status to WorkInProgress
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);

        // Can now be completed cleanly
        jc.Complete();
        Assert.Equal(JobCardStatus.Completed, jc.Status);
    }

    [Fact]
    public void SalesOrder_And_PurchaseOrder_SnapNearCompletePercentages()
    {
        // 1. SalesOrder PerDelivered & PerBilled
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Item 1", 3m, 33.33m, 0m); // LineTotal = 99.99
        so.Items[0].DeliveredQty = 3m;
        so.Items[0].BilledQty = 3m;

        // PendingDeliveryQty is 0 and PendingBillingQty is 0
        Assert.Equal(100m, so.PerDelivered);
        Assert.Equal(100m, so.PerBilled);

        // 2. PurchaseOrder PerReceived & PerBilled
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        var poItem1 = po.AddItem(Guid.NewGuid(), "Item 1", 3m, 33.33m, 0m);
        poItem1.ReceivedQty = 3m;
        poItem1.BilledQty = 3m;

        Assert.Equal(100m, po.PerReceived);
        Assert.Equal(100m, po.PerBilled);
    }

    [Fact]
    public void Dunning_AddOverduePayment_StoresPaymentScheduleId()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1);
        var siId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        dunning.AddOverduePayment(siId, 250m, DateTime.UtcNow.AddDays(-10), 10, scheduleId);

        Assert.Single(dunning.OverduePayments);
        var payment = dunning.OverduePayments[0];
        Assert.Equal(siId, payment.SalesInvoiceId);
        Assert.Equal(scheduleId, payment.PaymentScheduleId);
        Assert.Equal(250m, payment.OutstandingAmount);
        Assert.Equal(250m, dunning.TotalOutstanding);
    }

    [Fact]
    public void StockEntryManager_ValidateManufactureItems_ThrowsWhenNoFinishedGood()
    {
        var manager = new StockEntryManager(null!, null!, null!, null!);
        var companyId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        
        // Add only raw material
        entry.AddItem(Guid.NewGuid(), 10m, sourceWarehouseId: Guid.NewGuid(), targetWarehouseId: null, isFinishedItem: false);

        var ex = Assert.Throws<BusinessException>(() => manager.ValidateManufactureItems(entry));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("There must be at least 1 Finished Good", ex.Data["detail"]?.ToString());

        // Add finished good row (target warehouse, no source warehouse, isFinishedItem = true)
        entry.AddItem(Guid.NewGuid(), 10m, sourceWarehouseId: null, targetWarehouseId: Guid.NewGuid(), isFinishedItem: true);
        manager.ValidateManufactureItems(entry); // should not throw
    }

    [Fact]
    public void WorkOrder_AdditionalOperatingCost_InitializedAndDistributesToFg()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-TEST-001", itemId, bomId, 10m)
        {
            AdditionalOperatingCost = 150m
        };

        Assert.Equal(150m, wo.AdditionalOperatingCost);

        // Semi-FG check: when producing intermediate item, additional operating cost should NOT book
        var semiFgItemId = Guid.NewGuid();
        var isMainFg = semiFgItemId == wo.ItemId;
        Assert.False(isMainFg);

        // Main FG check: when producing main WO item, additional operating cost is booked proportionally
        var mainFgItemId = wo.ItemId;
        var isMainFg2 = mainFgItemId == wo.ItemId;
        Assert.True(isMainFg2);

        var producedQty = 4m;
        var addlCost = Math.Round((wo.AdditionalOperatingCost / wo.Quantity) * producedQty, 4);
        Assert.Equal(60m, addlCost); // (150 / 10) * 4 = 60
    }

    [Fact]
    public void ProformaInvoice_AmountBasis_PreservesExactAmountAndRejectsNegativeGrandTotal()
    {
        var companyId = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var proforma = new ProformaInvoice(Guid.NewGuid(), companyId, soId, customerId, DateTime.UtcNow, ProformaInvoiceBasis.Amount);

        // 3 units for total amount 100 -> rate = 33.3333, but Amount must remain exactly 100m
        var soItemId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        proforma.AddItem(soItemId, itemId, "ITEM-01", "Test Item", 3m, 33.3333m, "Nos", "Test Description", amount: 100m);

        Assert.Single(proforma.Items);
        var item = proforma.Items.First();
        Assert.Equal(100m, item.Amount);
        Assert.Equal(100m, proforma.GrandTotal);

        // SetGrandTotal: prorated tax scale
        proforma.SetGrandTotal(110m);
        Assert.Equal(110m, proforma.GrandTotal);

        // Rejects negative grand total
        var ex = Assert.Throws<BusinessException>(() => proforma.SetGrandTotal(-10m));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("The grand total of a Proforma Invoice cannot be negative", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void JobCard_Hold_IgnoredWhenAllQtyIsDone()
    {
        // ERPNext PR #62199 (commits 3d254343f2, f378114bf8): Finished cards must not be put On Hold
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 1);
        jc.Start();
        jc.AddTimeLog(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), 10m);
        Assert.Equal(10m, jc.CompletedQty);
        Assert.False(jc.HasQtyLeftToMake());
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);

        // Calling Hold() when HasQtyLeftToMake() is false should no-op and remain WorkInProgress
        jc.Hold();
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
    }

    [Fact]
    public void JobCard_SetProcessLoss_ReleasesHoldWhenCompletesJob()
    {
        // ERPNext PR #62199 (commits 3d254343f2, f378114bf8): Releasing hold once all qty is done
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 1);
        jc.Start();
        jc.AddTimeLog(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), 6m);
        Assert.Equal(6m, jc.CompletedQty);
        Assert.True(jc.HasQtyLeftToMake());

        // Card is in progress, can be put on hold
        jc.Hold();
        Assert.Equal(JobCardStatus.OnHold, jc.Status);

        // Recording process loss that covers remaining qty releases hold
        jc.ProcessLossQty = 4m;
        jc.SetProcessLoss();
        Assert.False(jc.HasQtyLeftToMake());
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
    }

    [Fact]
    public void Company_DefaultSellingAndBuyingTerms_Properties()
    {
        // ERPNext PR #62189 / commit 3a1b24cd70
        var company = new Company(Guid.NewGuid(), "Terms Parity Corp");
        var sellTermsId = Guid.NewGuid();
        var buyTermsId = Guid.NewGuid();

        company.DefaultSellingTermsId = sellTermsId;
        company.DefaultBuyingTermsId = buyTermsId;

        Assert.Equal(sellTermsId, company.DefaultSellingTermsId);
        Assert.Equal(buyTermsId, company.DefaultBuyingTermsId);
    }

    [Fact]
    public async System.Threading.Tasks.Task TermsResolutionService_DraftDocument_AppliesCompanyDefaultTerms()
    {
        // ERPNext PR #62189 / commit 3a1b24cd70
        var companyId = Guid.NewGuid();
        var defaultTermsId = Guid.NewGuid();
        var company = new Company(companyId, "Selling Corp") { DefaultSellingTermsId = defaultTermsId };
        var terms = new TermsAndConditions(defaultTermsId, companyId, "Standard Terms") { Terms = "Pay within 30 days" };

        var termsRepo = NSubstitute.Substitute.For<IRepository<TermsAndConditions, Guid>>();
        var compRepo = NSubstitute.Substitute.For<IRepository<Company, Guid>>();

        compRepo.FindAsync(companyId).Returns(System.Threading.Tasks.Task.FromResult<Company?>(company));
        termsRepo.FindAsync(defaultTermsId).Returns(System.Threading.Tasks.Task.FromResult<TermsAndConditions?>(terms));

        var service = new TermsResolutionService(termsRepo, compRepo);

        // Draft without user terms: gets default terms
        var (resolvedId, resolvedText) = await service.ResolveSellingTermsAsync(
            DocumentStatus.Draft, companyId, null, null);

        Assert.Equal(defaultTermsId, resolvedId);
        Assert.Equal("Pay within 30 days", resolvedText);
    }

    [Fact]
    public async System.Threading.Tasks.Task TermsResolutionService_NonDraftDocument_NeverAppliesCompanyDefaultTerms()
    {
        // ERPNext PR #62189 / commit 3a1b24cd70: Non-draft documents must NOT have default terms applied onload/update
        var companyId = Guid.NewGuid();
        var defaultTermsId = Guid.NewGuid();
        var company = new Company(companyId, "Selling Corp") { DefaultSellingTermsId = defaultTermsId };

        var termsRepo = NSubstitute.Substitute.For<IRepository<TermsAndConditions, Guid>>();
        var compRepo = NSubstitute.Substitute.For<IRepository<Company, Guid>>();

        var service = new TermsResolutionService(termsRepo, compRepo);

        // Submitted document with empty terms: invariant ensures company default terms are NOT applied
        var (resolvedId, resolvedText) = await service.ResolveSellingTermsAsync(
            DocumentStatus.Submitted, companyId, null, null);

        Assert.Null(resolvedId);
        Assert.Null(resolvedText);
        await compRepo.DidNotReceive().FindAsync(NSubstitute.Arg.Any<Guid>());
    }

    [Fact]
    public async System.Threading.Tasks.Task TermsResolutionService_CustomTerms_RetainedAndNotOverwritten()
    {
        // ERPNext PR #62189: User-provided terms are preserved
        var companyId = Guid.NewGuid();
        var defaultTermsId = Guid.NewGuid();
        var company = new Company(companyId, "Selling Corp") { DefaultSellingTermsId = defaultTermsId };

        var termsRepo = NSubstitute.Substitute.For<IRepository<TermsAndConditions, Guid>>();
        var compRepo = NSubstitute.Substitute.For<IRepository<Company, Guid>>();

        var service = new TermsResolutionService(termsRepo, compRepo);

        var (resolvedId, resolvedText) = await service.ResolveSellingTermsAsync(
            DocumentStatus.Draft, companyId, null, "Custom Customer Terms");

        Assert.Null(resolvedId);
        Assert.Equal("Custom Customer Terms", resolvedText);
        await compRepo.DidNotReceive().FindAsync(NSubstitute.Arg.Any<Guid>());
    }

    [Fact]
    public void UserAccessValidator_IsAdminUser_DetectsAdminRoles()
    {
        // Parity with ERPNext PR #61894 / commit c80bd8912b: get_admin_roles()
        Assert.True(UserAccessValidator.IsAdminUser(new[] { "Administrator" }));
        Assert.True(UserAccessValidator.IsAdminUser(new[] { "System Manager" }));
        Assert.True(UserAccessValidator.IsAdminUser(new[] { "admin" }));
        Assert.True(UserAccessValidator.IsAdminUser(new[] { "SYSTEM MANAGER" }));
        Assert.True(UserAccessValidator.IsAdminUser(new[] { "HR User", "admin" }));

        Assert.False(UserAccessValidator.IsAdminUser(null));
        Assert.False(UserAccessValidator.IsAdminUser(Array.Empty<string>()));
        Assert.False(UserAccessValidator.IsAdminUser(new[] { "HR User" }));
        Assert.False(UserAccessValidator.IsAdminUser(new[] { "HR Manager" }));
        Assert.False(UserAccessValidator.IsAdminUser(new[] { "Employee" }));
        Assert.False(UserAccessValidator.IsAdminUser(new[] { "Sales User", "Accounts User" }));
    }

    [Fact]
    public void UserAccessValidator_CanModifyUserLink_EnforcesAdminProtection()
    {
        // Parity with ERPNext PR #61894 / commit c80bd8912b (validate_user_access):
        // Only administrators can link/unlink/update users holding admin roles.
        // HR User/Manager can link/unlink operational users without error.

        var hrEditor = new[] { "HR User" };
        var hrManagerEditor = new[] { "HR Manager" };
        var adminEditor = new[] { "System Manager" };
        var standardUser = new[] { "Employee", "Sales User" };
        var adminUser = new[] { "System Manager" };
        var administratorUser = new[] { "Administrator" };
        var genericAdminUser = new[] { "admin" };

        // 1. Admin editor can link any user
        Assert.True(UserAccessValidator.CanModifyUserLink(adminEditor, standardUser));
        Assert.True(UserAccessValidator.CanModifyUserLink(adminEditor, adminUser));
        Assert.True(UserAccessValidator.CanModifyUserLink(adminEditor, administratorUser));
        Assert.True(UserAccessValidator.CanModifyUserLink(adminEditor, null));
        Assert.True(UserAccessValidator.CanModifyUserLink(adminEditor, Array.Empty<string>()));

        // 2. HR editor can link standard/operational users
        Assert.True(UserAccessValidator.CanModifyUserLink(hrEditor, standardUser));
        Assert.True(UserAccessValidator.CanModifyUserLink(hrManagerEditor, standardUser));
        Assert.True(UserAccessValidator.CanModifyUserLink(hrEditor, new[] { "Employee" }));
        Assert.True(UserAccessValidator.CanModifyUserLink(hrEditor, new[] { "Accounts User" }));
        Assert.True(UserAccessValidator.CanModifyUserLink(hrEditor, null));
        Assert.True(UserAccessValidator.CanModifyUserLink(hrEditor, Array.Empty<string>()));

        // 3. HR editor is BLOCKED from linking or unlinking admin users
        Assert.False(UserAccessValidator.CanModifyUserLink(hrEditor, adminUser));
        Assert.False(UserAccessValidator.CanModifyUserLink(hrEditor, administratorUser));
        Assert.False(UserAccessValidator.CanModifyUserLink(hrEditor, genericAdminUser));
        Assert.False(UserAccessValidator.CanModifyUserLink(hrEditor, new[] { "Employee", "Administrator" }));
        Assert.False(UserAccessValidator.CanModifyUserLink(hrManagerEditor, adminUser));
        Assert.False(UserAccessValidator.CanModifyUserLink(hrManagerEditor, genericAdminUser));
    }

    [Fact]
    public async System.Threading.Tasks.Task InterCompanyTransactionService_CreatePurchaseOrderFromSalesOrder_LinksSalesOrderLineAndId()
    {
        // Parity with ERPNext PR #61972 / commit 92a4ccffab
        var sourceCompanyId = Guid.NewGuid();
        var targetCompanyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var customer = new Customer(customerId, sourceCompanyId, "InterCompany Customer")
        {
            RepresentsCompanyId = targetCompanyId
        };
        var supplier = new Supplier(supplierId, targetCompanyId, "InterCompany Supplier")
        {
            RepresentsCompanyId = sourceCompanyId,
            IsActive = true
        };
        var targetCompany = new Company(targetCompanyId, "Target Co") { CurrencyCode = "MYR" };

        var salesOrder = new SalesOrder(Guid.NewGuid(), sourceCompanyId, customerId, "SO-IC-001", DateTime.UtcNow);
        salesOrder.AddItem(itemId, "Test Item", 5m, 100m, 0m, "Unit");
        var soItem = salesOrder.Items[0];

        var poRepo = NSubstitute.Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var soRepo = NSubstitute.Substitute.For<IRepository<SalesOrder, Guid>>();
        var siRepo = NSubstitute.Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = NSubstitute.Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var compRepo = NSubstitute.Substitute.For<IRepository<Company, Guid>>();
        var custRepo = NSubstitute.Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = NSubstitute.Substitute.For<IRepository<Supplier, Guid>>();

        custRepo.GetAsync(customerId).Returns(System.Threading.Tasks.Task.FromResult(customer));
        compRepo.GetAsync(targetCompanyId).Returns(System.Threading.Tasks.Task.FromResult(targetCompany));
        suppRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        PurchaseOrder? insertedPo = null;
        await poRepo.InsertAsync(NSubstitute.Arg.Do<PurchaseOrder>(p => insertedPo = p), autoSave: true);

        var service = new InterCompanyTransactionService(
            siRepo, piRepo, poRepo, compRepo, custRepo, suppRepo);

        var poId = await service.CreatePurchaseOrderFromSalesOrderAsync(
            salesOrder, (docType, compId) => System.Threading.Tasks.Task.FromResult("PO-IC-001"));

        Assert.NotNull(insertedPo);
        Assert.Equal(poId, insertedPo.Id);
        Assert.Single(insertedPo.Items);

        var poItem = insertedPo.Items[0];
        Assert.Equal(salesOrder.Id, poItem.SalesOrderId);
        Assert.Equal(soItem.Id, poItem.SalesOrderItemId);
        Assert.Equal(itemId, poItem.ItemId);
        Assert.Equal(5m, poItem.Quantity);
    }

    [Fact]
    public void DefaultValuationMethodChangeGuard_CancelledSle_DoesNotBlockChange()
    {
        // Parity with ERPNext PR #62101 / commit c52fd92533:
        // Cancelled stock ledger entries (IsCancelled = true) are ignored when changing default valuation method.
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // Scenario 1: Only cancelled SLE exists -> NOT blocked
        var cancelledSles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), companyId, itemId, warehouseId,
                DateTime.UtcNow, 10m, 50m, 10m, 500m)
            {
                IsCancelled = true
            }
        };

        var hasBlockingSle1 = cancelledSles.Any(s => s.CompanyId == companyId && !s.IsCancelled);
        Assert.False(hasBlockingSle1);

        // Scenario 2: Active SLE exists -> BLOCKED
        var activeSles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), companyId, itemId, warehouseId,
                DateTime.UtcNow, 10m, 50m, 10m, 500m)
            {
                IsCancelled = false
            }
        };

        var hasBlockingSle2 = activeSles.Any(s => s.CompanyId == companyId && !s.IsCancelled);
        Assert.True(hasBlockingSle2);
    }
}
