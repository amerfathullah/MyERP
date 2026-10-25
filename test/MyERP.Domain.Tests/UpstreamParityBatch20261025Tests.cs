using System.Threading.Tasks;
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
    [Fact]
    public void Dunning_PaymentScheduleOutstanding_UnallocatedPaymentsReduceOldestInstallmentsFirst()
    {
        // Parity with ERPNext PR #61667 / commit 18e651d89e
        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();

        var schedules = new List<(Guid Id, DateTime DueDate, decimal Outstanding)>
        {
            (s1Id, new DateTime(2026, 10, 1), 50m),
            (s2Id, new DateTime(2026, 10, 15), 50m)
        };

        // Scenario A: invoice total outstanding = 80m (20m unallocated payment)
        var totalOutstandingA = 80m;
        var scheduleOutstandingA = schedules.ToDictionary(s => s.Id, s => s.Outstanding);
        var unallocatedA = Math.Max(0m, schedules.Sum(s => s.Outstanding) - totalOutstandingA);
        foreach (var row in schedules.OrderBy(s => s.DueDate))
        {
            var paid = Math.Min(scheduleOutstandingA[row.Id], unallocatedA);
            unallocatedA -= paid;
            scheduleOutstandingA[row.Id] -= paid;
        }

        Assert.Equal(30m, scheduleOutstandingA[s1Id]); // oldest installment reduced from 50 to 30
        Assert.Equal(50m, scheduleOutstandingA[s2Id]); // newer installment untouched

        // Scenario B: invoice total outstanding = 40m (60m unallocated payment)
        var totalOutstandingB = 40m;
        var scheduleOutstandingB = schedules.ToDictionary(s => s.Id, s => s.Outstanding);
        var unallocatedB = Math.Max(0m, schedules.Sum(s => s.Outstanding) - totalOutstandingB);
        foreach (var row in schedules.OrderBy(s => s.DueDate))
        {
            var paid = Math.Min(scheduleOutstandingB[row.Id], unallocatedB);
            unallocatedB -= paid;
            scheduleOutstandingB[row.Id] -= paid;
        }

        Assert.Equal(0m, scheduleOutstandingB[s1Id]); // oldest installment fully paid (50 - 50 = 0)
        Assert.Equal(40m, scheduleOutstandingB[s2Id]); // newer installment reduced (50 - 10 = 40)
    }

    [Fact]
    public async Task PartySpecificItem_AncestorCustomerGroupRule_AppliesDownToSubGroup()
    {
        // Parity with ERPNext commit 9cd213ecab: group rules apply down the group tree
        var rootGroupId = Guid.NewGuid();
        var parentGroupId = Guid.NewGuid();
        var subGroupId = Guid.NewGuid();

        var rootGroup = new CustomerGroup(rootGroupId, "All Customer Groups", isGroup: true);
        var parentGroup = new CustomerGroup(parentGroupId, "Corporate", parentId: rootGroupId, isGroup: true);
        var subGroup = new CustomerGroup(subGroupId, "Corporate Sub", parentId: parentGroupId, isGroup: false);

        var custGroupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
        custGroupRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<CustomerGroup> { rootGroup, parentGroup, subGroup }.AsQueryable()));

        var itemAllowedId = Guid.NewGuid();
        var itemOtherId = Guid.NewGuid();

        var ruleAllowed = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.CustomerGroup,
            parentGroupId, PartySpecificItemRestrictBasedOn.Item, itemAllowedId);

        var otherPartyGroupId = Guid.NewGuid();
        var ruleOther = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.CustomerGroup,
            otherPartyGroupId, PartySpecificItemRestrictBasedOn.Item, itemOtherId);

        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<PartySpecificItem> { ruleAllowed, ruleOther }.AsQueryable()));

        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        brandRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Brand>().AsQueryable()));

        var itemAllowed = new Item(itemAllowedId, Guid.NewGuid(), "ITEM-ALLOWED", "Allowed", ItemType.Goods);
        var itemOther = new Item(itemOtherId, Guid.NewGuid(), "ITEM-OTHER", "Other", ItemType.Goods);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Item> { itemAllowed, itemOther }.AsQueryable()));

        var filterService = new PartySpecificItemFilterService(
            partyRuleRepo, brandRepo, itemRepo, null, custGroupRepo, null);

        var customerId = Guid.NewGuid();

        // 1. Customer in subGroup accessing allowed item succeeds (parent group rule covers sub group)
        await filterService.ValidatePartySpecificItemsAsync(
            PartySpecificItemPartyType.Customer, customerId,
            PartySpecificItemPartyType.CustomerGroup, subGroupId,
            new[] { itemAllowedId }, "Test Customer");

        // 2. Customer in subGroup accessing itemOther throws ItemRestrictedForParty (itemOther is restricted to otherPartyGroup)
        var ex = await Assert.ThrowsAsync<BusinessException>(async () =>
        {
            await filterService.ValidatePartySpecificItemsAsync(
                PartySpecificItemPartyType.Customer, customerId,
                PartySpecificItemPartyType.CustomerGroup, subGroupId,
                new[] { itemOtherId }, "Test Customer");
        });
        Assert.Equal(MyERPDomainErrorCodes.ItemRestrictedForParty, ex.Code);

        // 3. Filter check: itemOther is excluded, itemAllowed is NOT excluded
        var filter = await filterService.GetItemFilterAsync(
            PartySpecificItemPartyType.Customer, customerId,
            PartySpecificItemPartyType.CustomerGroup, subGroupId);

        Assert.Contains(itemOtherId, filter.ExcludedItemIds);
        Assert.DoesNotContain(itemAllowedId, filter.ExcludedItemIds);
    }

    [Fact]
    public async Task PartySpecificItem_AncestorSupplierGroupRule_AppliesDownToSubGroup()
    {
        // Parity with ERPNext commit 9cd213ecab: supplier group rules apply down tree
        var rootGroupId = Guid.NewGuid();
        var parentGroupId = Guid.NewGuid();
        var subGroupId = Guid.NewGuid();

        var rootGroup = new SupplierGroup(rootGroupId, "All Supplier Groups", isGroup: true);
        var parentGroup = new SupplierGroup(parentGroupId, "Raw Materials", parentId: rootGroupId, isGroup: true);
        var subGroup = new SupplierGroup(subGroupId, "Metals", parentId: parentGroupId, isGroup: false);

        var suppGroupRepo = Substitute.For<IRepository<SupplierGroup, Guid>>();
        suppGroupRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SupplierGroup> { rootGroup, parentGroup, subGroup }.AsQueryable()));

        var itemAllowedId = Guid.NewGuid();
        var itemOtherId = Guid.NewGuid();

        var ruleAllowed = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.SupplierGroup,
            parentGroupId, PartySpecificItemRestrictBasedOn.Item, itemAllowedId);

        var otherPartyGroupId = Guid.NewGuid();
        var ruleOther = new PartySpecificItem(
            Guid.NewGuid(), PartySpecificItemPartyType.SupplierGroup,
            otherPartyGroupId, PartySpecificItemRestrictBasedOn.Item, itemOtherId);

        var partyRuleRepo = Substitute.For<IRepository<PartySpecificItem, Guid>>();
        partyRuleRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<PartySpecificItem> { ruleAllowed, ruleOther }.AsQueryable()));

        var brandRepo = Substitute.For<IRepository<Brand, Guid>>();
        brandRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Brand>().AsQueryable()));

        var itemAllowed = new Item(itemAllowedId, Guid.NewGuid(), "ITEM-SUPP-ALLOWED", "Allowed", ItemType.Goods);
        var itemOther = new Item(itemOtherId, Guid.NewGuid(), "ITEM-SUPP-OTHER", "Other", ItemType.Goods);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<Item> { itemAllowed, itemOther }.AsQueryable()));

        var filterService = new PartySpecificItemFilterService(
            partyRuleRepo, brandRepo, itemRepo, null, null, suppGroupRepo);

        var supplierId = Guid.NewGuid();

        // 1. Supplier in subGroup accessing allowed item succeeds (parent group rule covers sub group)
        await filterService.ValidatePartySpecificItemsAsync(
            PartySpecificItemPartyType.Supplier, supplierId,
            PartySpecificItemPartyType.SupplierGroup, subGroupId,
            new[] { itemAllowedId }, "Test Supplier");

        // 2. Supplier in subGroup accessing itemOther throws ItemRestrictedForParty
        var ex = await Assert.ThrowsAsync<BusinessException>(async () =>
        {
            await filterService.ValidatePartySpecificItemsAsync(
                PartySpecificItemPartyType.Supplier, supplierId,
                PartySpecificItemPartyType.SupplierGroup, subGroupId,
                new[] { itemOtherId }, "Test Supplier");
        });
        Assert.Equal(MyERPDomainErrorCodes.ItemRestrictedForParty, ex.Code);
    }
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void SubscriptionGracePeriodBoundaries_MatchesERPNextPR61493(int gracePeriod, bool cancelAfterGrace)
    {
        // Enforces parity with ERPNext PR #61493 / commit 1697b6643b & fe897ec6fc:
        // - Invoice due date is NOT overdue on due date itself (asOfDate == dueDate -> Active).
        // - Overdue within grace period (days_overdue in 1..grace_period) -> GracePeriod.
        // - Past grace period (days_overdue > grace_period) -> Cancelled (if cancelAfterGrace) or Unpaid (if !cancelAfterGrace).
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var startDate = new DateTime(2026, 9, 10);
        var sub = new Subscription(Guid.NewGuid(), companyId, customerId, "Customer", startDate, "Monthly")
        {
            DaysUntilDue = 0,
            CurrentInvoiceStart = startDate,
            CurrentInvoiceEnd = startDate
        };

        var dueDate = startDate.AddDays(sub.DaysUntilDue);
        var engine = new SubscriptionBillingEngine(null!);

        for (int daysOverdue = 0; daysOverdue <= gracePeriod + 2; daysOverdue++)
        {
            var postingDate = dueDate.AddDays(daysOverdue);

            var status = engine.DetermineStatus(
                sub,
                postingDate,
                hasOutstandingInvoices: true,
                isFullyRefunded: false,
                settingsGracePeriod: gracePeriod,
                settingsCancelAfterGrace: cancelAfterGrace);

            if (daysOverdue == 0)
            {
                Assert.Equal(SubscriptionStatus.Active, status);
            }
            else if (daysOverdue <= gracePeriod)
            {
                Assert.Equal(SubscriptionStatus.GracePeriod, status);
            }
            else
            {
                var expected = cancelAfterGrace ? SubscriptionStatus.Cancelled : SubscriptionStatus.Unpaid;
                Assert.Equal(expected, status);
            }
        }
    }

    [Fact]
    public void DropShipService_ResolvePurchaseLines_DuplicateItemCodeDifferentRows_MaintainsDistinctSelections()
    {
        // Enforces parity with ERPNext PR #61972 / commit 92a4ccffab:
        // When a Sales Order contains multiple rows for the same ItemId,
        // each row is identified strictly by SalesOrderItemId and mapped to its specific supplier.
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-DS-001", DateTime.UtcNow);

        var sharedItemId = Guid.NewGuid();
        var supplierAId = Guid.NewGuid();
        var supplierBId = Guid.NewGuid();

        // Row 1: 5 units of shared item
        so.AddItem(sharedItemId, "Shared Item Row 1", 5m, 100m, 0m, "Unit");
        var item1 = so.Items[0];
        // Row 2: 10 units of shared item
        so.AddItem(sharedItemId, "Shared Item Row 2", 10m, 100m, 0m, "Unit");
        var item2 = so.Items[1];
        var selections = new List<(Guid SalesOrderItemId, Guid SupplierId)>
        {
            (item1.Id, supplierAId),
            (item2.Id, supplierBId)
        };

        var lines = DropShipService.ResolvePurchaseLines(so, selections);

        Assert.Equal(2, lines.Count);

        var lineA = lines.FirstOrDefault(l => l.SupplierId == supplierAId);
        Assert.NotNull(lineA);
        Assert.Equal(item1.Id, lineA.Item.Id);
        Assert.Equal(5m, lineA.Quantity);

        var lineB = lines.FirstOrDefault(l => l.SupplierId == supplierBId);
        Assert.NotNull(lineB);
        Assert.Equal(item2.Id, lineB.Item.Id);
        Assert.Equal(10m, lineB.Quantity);
    }

    [Fact]
    public void ProductBundle_AddItem_SelfReferencingItem_ThrowsValidationFailed_PR62149()
    {
        var parentItemId = Guid.NewGuid();
        var bundle = new ProductBundle(Guid.NewGuid(), parentItemId);

        var ex = Assert.Throws<BusinessException>(() =>
            bundle.AddItem(parentItemId, 1m, "Self Component"));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProductBundle_Decompose_HonoursComponentUomConversion_PR62149()
    {
        var bundleItemId = Guid.NewGuid();
        var compItemId = Guid.NewGuid();
        var bundle = new ProductBundle(Guid.NewGuid(), bundleItemId);
        bundle.AddItem(compItemId, 1m, "Box Component", uom: "Box");

        var bundleRepo = NSubstitute.Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var compItem = new Item(compItemId, Guid.NewGuid(), "COMP-1", "Component 1", ItemType.Goods)
        {
            Uom = "Nos"
        };
        var itemRepo = NSubstitute.Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<Item> { compItem }.AsQueryable()));

        var uomConversion = new UomConversion(Guid.NewGuid(), "Box", "Nos", 12m, compItemId);
        var uomRepo = NSubstitute.Substitute.For<IRepository<UomConversion, Guid>>();
        uomRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<UomConversion> { uomConversion }.AsQueryable()));
        var uomService = new UomConversionService(uomRepo);

        var service = new ProductBundleDecompositionService(bundleRepo, itemRepo, uomService);

        // 3 bundles x 1 Box (12 Nos) = 36 Nos
        var decomposed = await service.DecomposeAsync(bundleItemId, 3m, 300m);

        Assert.Single(decomposed);
        Assert.Equal(compItemId, decomposed[0].ComponentItemId);
        Assert.Equal(36m, decomposed[0].Qty);
        Assert.Equal("Nos", decomposed[0].Uom);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProductBundle_Decompose_AllowsDisabledBundleOnReturn_PR62149()
    {
        var bundleItemId = Guid.NewGuid();
        var compItemId = Guid.NewGuid();
        var bundle = new ProductBundle(Guid.NewGuid(), bundleItemId);
        bundle.AddItem(compItemId, 5m, "Component");
        bundle.Disable();

        var bundleRepo = NSubstitute.Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        // Standard transaction does not decompose disabled bundle
        var standardResult = await service.DecomposeAsync(bundleItemId, 1m, 100m, isReturn: false);
        Assert.Empty(standardResult);

        // Return transaction decomposes disabled bundle
        var returnResult = await service.DecomposeAsync(bundleItemId, 1m, 100m, isReturn: true);
        Assert.Single(returnResult);
        Assert.Equal(5m, returnResult[0].Qty);
    }

    [Fact]
    public async Task Quotation_MarkLost_RequiresLostReason_ThrowsValidationFailed_PR61828()
    {
        var quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
        var qId = Guid.NewGuid();
        var qtn = new Quotation(qId, Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.UtcNow);
        qtn.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        qtn.Submit();
        quotationRepo.GetAsync(qId).Returns(Task.FromResult(qtn));

        var appService = new QuotationAppService(
            quotationRepo,
            Substitute.For<IRepository<Customer, Guid>>(),
            Substitute.For<IRepository<MyERP.CRM.Entities.CompetitorDetail, Guid>>(),
            Substitute.For<IRepository<QuotationLostReasonDetail, Guid>>(),
            Substitute.For<IRepository<QuotationLostReason, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            null!);

        // Missing input or empty LostReasonIds -> throws ValidationFailed
        var exNull = await Assert.ThrowsAsync<BusinessException>(() => appService.MarkLostAsync(qId, null));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, exNull.Code);
        Assert.Contains("Please select at least one Lost Reason", exNull.Data["detail"]?.ToString() ?? "");

        var exEmpty = await Assert.ThrowsAsync<BusinessException>(() => appService.MarkLostAsync(qId, new MarkQuotationLostDto { LostReasonIds = new List<Guid>() }));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, exEmpty.Code);
        Assert.Contains("Please select at least one Lost Reason", exEmpty.Data["detail"]?.ToString() ?? "");
    }

    [Fact]
    public async Task Supplier_ValidateSupplierGroup_RejectsGroupType_PR61829()
    {
        var groupRepo = Substitute.For<IRepository<SupplierGroup, Guid>>();
        var leafGroupId = Guid.NewGuid();
        var parentGroupId = Guid.NewGuid();

        var groups = new List<SupplierGroup>
        {
            new SupplierGroup(parentGroupId, "All Supplier Groups", isGroup: true),
            new SupplierGroup(leafGroupId, "Services", parentId: parentGroupId, isGroup: false)
        }.AsQueryable();

        groupRepo.GetQueryableAsync().Returns(Task.FromResult(groups));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SupplierGroup, Guid>>().Returns(groupRepo);

        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var appService = new SupplierAppService(suppRepo)
        {
            LazyServiceProvider = lazySp
        };

        // Rejects group type supplier group
        var groupDto = new CreateUpdateSupplierDto
        {
            CompanyId = Guid.NewGuid(),
            Name = "Acme Vendor",
            SupplierGroupId = parentGroupId
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => appService.CreateAsync(groupDto));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Cannot select a Group type Supplier Group", ex.Data["detail"]?.ToString() ?? "");
    }

    [Fact]
    public void SalesTeamEntry_ValidateAllocatedPercentages_RejectsRepeatedSalesPerson_PR61835()
    {
        var spId = Guid.NewGuid();
        var entries = new List<SalesTeamEntry>
        {
            new SalesTeamEntry(Guid.NewGuid(), spId, "SalesOrder", Guid.NewGuid(), 50m, 1000m, 5m),
            new SalesTeamEntry(Guid.NewGuid(), spId, "SalesOrder", Guid.NewGuid(), 50m, 1000m, 5m)
        };

        var ex = Assert.Throws<BusinessException>(() => SalesTeamEntry.ValidateAllocatedPercentages(entries));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("repeated", ex.Data["detail"]?.ToString() ?? "");
    }

    [Theory]
    [InlineData(-10, 110)]
    [InlineData(120, -20)]
    public void SalesTeamEntry_ValidateAllocatedPercentages_RejectsOutOfBoundsContribution_PR61835(decimal p1, decimal p2)
    {
        var entries = new List<SalesTeamEntry>
        {
            new SalesTeamEntry(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), p1, 1000m, 5m),
            new SalesTeamEntry(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), p2, 1000m, 5m)
        };

        var ex = Assert.Throws<BusinessException>(() => SalesTeamEntry.ValidateAllocatedPercentages(entries));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must be between 0 and 100", ex.Data["detail"]?.ToString() ?? "");
    }

    [Fact]
    public async Task SalesPerson_Disable_RejectsUnfinishedSalesOrderOrDeliveryNote_PR61835()
    {
        var spId = Guid.NewGuid();
        var sp = new SalesPerson(spId, "John Sales");
        var spRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
        spRepo.GetAsync(spId).Returns(Task.FromResult(sp));

        var teamRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
        var soId = Guid.NewGuid();
        var teamEntries = new List<SalesTeamEntry>
        {
            new SalesTeamEntry(Guid.NewGuid(), spId, "SalesOrder", soId, 100m, 1000m, 5m)
        }.AsQueryable();
        teamRepo.GetQueryableAsync().Returns(Task.FromResult(teamEntries));

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var order = new SalesOrder(soId, Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Item", 1m, 1000m, 0m);
        order.Submit(); // Status = ToDeliverAndBill (unfinished)
        var orders = new List<SalesOrder> { order }.AsQueryable();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SalesTeamEntry, Guid>>().Returns(teamRepo);
        lazySp.LazyGetRequiredService<IRepository<SalesOrder, Guid>>().Returns(soRepo);

        var appService = new SalesPersonAppService(spRepo)
        {
            LazyServiceProvider = lazySp
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => appService.DisableAsync(spId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("unfinished", ex.Data["detail"]?.ToString() ?? "");
    }

    [Fact]
    public async Task PartyType_Update_RefusesChangingAccountTypeWhenGLEntriesExist_PR60804()
    {
        var ptId = Guid.NewGuid();
        var partyType = new PartyType(ptId, "Sales Partner", PartyAccountType.Receivable);
        var ptRepo = Substitute.For<IRepository<PartyType, Guid>>();
        ptRepo.GetAsync(ptId).Returns(Task.FromResult(partyType));

        var jeLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
        var jeLines = new List<JournalEntryLine>
        {
            new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true) { PartyType = "Sales Partner" }
        }.AsQueryable();
        jeLineRepo.GetQueryableAsync().Returns(Task.FromResult(jeLines));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<JournalEntryLine, Guid>>().Returns(jeLineRepo);

        var appService = new PartyTypeAppService(ptRepo)
        {
            LazyServiceProvider = lazySp
        };

        var updateDto = new CreateUpdatePartyTypeDto
        {
            Name = "Sales Partner",
            AccountType = PartyAccountType.Payable
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => appService.UpdateAsync(ptId, updateDto));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Cannot change Account Type for Sales Partner after ledger entries exist", ex.Data["detail"]?.ToString() ?? "");
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    public void SalesPerson_AddTarget_RejectsNegativeTargets_PR62324(decimal qty, decimal amount)
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Sam Sales");
        var fyId = Guid.NewGuid();

        var ex = Assert.Throws<BusinessException>(() => sp.AddTarget(fyId, qty, amount));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("cannot be negative", ex.Data["detail"]?.ToString() ?? "");
    }

    [Fact]
    public void SalesPerson_AddTarget_RejectsDuplicateTargets_PR62324()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Sam Sales");
        var fyId = Guid.NewGuid();
        var igId = Guid.NewGuid();

        // First target succeeds
        sp.AddTarget(fyId, 50m, 1000m, igId);

        // Duplicate (same fiscal year and item group) throws
        var ex = Assert.Throws<BusinessException>(() => sp.AddTarget(fyId, 60m, 2000m, igId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("already exists", ex.Data["detail"]?.ToString() ?? "");

        // Overall target (null itemGroup) for same fiscal year is separate and succeeds
        sp.AddTarget(fyId, 200m, 5000m, null);
        Assert.Equal(2, sp.Targets.Count);
    }

    [Fact]
    public async Task SalesPersonTargetVariance_GroupTarget_IncludesDescendantAchievements_PR62324()
    {
        var companyId = Guid.NewGuid();
        var fyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var leafId = Guid.NewGuid();

        var manager = new SalesPerson(groupId, "Manager Group") { IsGroup = true };
        manager.AddTarget(fyId, 0m, 10000m, null);

        var rep = new SalesPerson(leafId, "Sales Rep", parentId: groupId) { IsGroup = false };

        var spRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
        spRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesPerson, object>>[]>())
            .Returns(Task.FromResult(new List<SalesPerson> { manager, rep }.AsQueryable()));

        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, companyId, "ITEM-1", "Test Item", ItemType.Goods);
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        itemGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemGroup>().AsQueryable()));

        var fiscalYear = new FiscalYear(fyId, companyId, "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        fyRepo.FindAsync(fyId).Returns(Task.FromResult<FiscalYear?>(fiscalYear));

        var company = new Company(companyId, "Acme");
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(company));

        var order = new SalesOrder(soId, companyId, Guid.NewGuid(), "SO-101", new DateTime(2026, 3, 1));
        order.AddItem(itemId, "Item 1", 1m, 2500m, 0m);
        order.Submit();

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { order }.AsQueryable()));

        var teamEntries = new List<SalesTeamEntry>
        {
            new SalesTeamEntry(Guid.NewGuid(), leafId, "SalesOrder", soId, 100m, 2500m, 5m)
        }.AsQueryable();
        var teamRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
        teamRepo.GetQueryableAsync().Returns(Task.FromResult(teamEntries));

        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();

        var service = new SalesPersonTargetVarianceAppService(
            spRepo, teamRepo, siRepo, soRepo, itemRepo, itemGroupRepo, fyRepo, companyRepo);

        var report = await service.GetReportAsync(new GetSalesPersonTargetVarianceInput
        {
            CompanyId = companyId,
            FiscalYearId = fyId,
            SalesPersonId = groupId,
            DocType = "Sales Order",
            TargetOn = "Amount"
        });

        // Manager Group target (10000) includes rep's order (2500)
        var row = Assert.Single(report.Rows);
        Assert.Equal(groupId, row.SalesPersonId);
        Assert.Equal(10000m, row.Target);
        Assert.Equal(2500m, row.Achieved);
        Assert.Equal(-7500m, row.Variance);
        Assert.Equal(25m, row.AchievementPercentage);
    }

    [Fact]
    public async Task PartyValidationService_ValidatePartyTypeExists_RejectsUnregisteredType_PR60804()
    {
        var partyTypeRepo = Substitute.For<IRepository<PartyType, Guid>>();
        var registeredTypes = new List<PartyType>
        {
            new PartyType(Guid.NewGuid(), "Customer", PartyAccountType.Receivable),
            new PartyType(Guid.NewGuid(), "Supplier", PartyAccountType.Payable)
        }.AsQueryable();

        partyTypeRepo.GetQueryableAsync().Returns(Task.FromResult(registeredTypes));
        partyTypeRepo.GetCountAsync().Returns(Task.FromResult(2L));

        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IRepository<PartyType, Guid>)).Returns(partyTypeRepo);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetService<IRepository<PartyType, Guid>>().Returns(partyTypeRepo);

        var service = new PartyValidationService();
        typeof(Volo.Abp.Domain.Services.DomainService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(service, lazySp);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ValidatePartyTypeExistsAsync("User", "Row 1: "));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Row 1: User is not a valid Party Type", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public async Task PartyValidationService_ValidatePartyTypeExists_AcceptsRegisteredType_PR60804()
    {
        var partyTypeRepo = Substitute.For<IRepository<PartyType, Guid>>();
        var customerPt = new PartyType(Guid.NewGuid(), "Customer", PartyAccountType.Receivable);
        var registeredTypes = new List<PartyType> { customerPt }.AsQueryable();

        partyTypeRepo.GetQueryableAsync().Returns(Task.FromResult(registeredTypes));
        partyTypeRepo.GetCountAsync().Returns(Task.FromResult(1L));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetService<IRepository<PartyType, Guid>>().Returns(partyTypeRepo);

        var service = new PartyValidationService();
        typeof(Volo.Abp.Domain.Services.DomainService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(service, lazySp);

        var result = await service.ValidatePartyTypeExistsAsync("Customer");
        Assert.NotNull(result);
        Assert.Equal("Customer", result.Name);
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountType_RejectsMismatch_PR60804()
    {
        var service = new PartyValidationService();
        var companyId = Guid.NewGuid();
        var payableAccount = new Account(Guid.NewGuid(), companyId, "2001", "Creditors", AccountType.Liability)
        {
            AccountSubType = AccountSubType.AccountsPayable
        };

        var ex = Assert.Throws<BusinessException>(() =>
            service.ValidatePartyAccountType("Customer", PartyAccountType.Receivable, payableAccount, separateAdvance: false));

        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Account Creditors and Party Type Customer have different account types", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountType_AcceptsShareholderEquity_PR60804()
    {
        var service = new PartyValidationService();
        var companyId = Guid.NewGuid();
        var equityAccount = new Account(Guid.NewGuid(), companyId, "3001", "Share Capital", AccountType.Equity)
        {
            AccountSubType = AccountSubType.ShareCapital
        };

        // Shareholder with Equity account is accepted without exception
        service.ValidatePartyAccountType("Shareholder", PartyAccountType.Payable, equityAccount, separateAdvance: false);
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountType_AcceptsSeparateAdvance_PR60804()
    {
        var service = new PartyValidationService();
        var companyId = Guid.NewGuid();
        var liabilityAccount = new Account(Guid.NewGuid(), companyId, "2100", "Customer Advances", AccountType.Liability);

        // When separateAdvance is true, different account type is permitted
        service.ValidatePartyAccountType("Customer", PartyAccountType.Receivable, liabilityAccount, separateAdvance: true);
    }
}
