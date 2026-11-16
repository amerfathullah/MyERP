using MyERP.Purchasing;
using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.SubcontractingAndWiringTests;

public class SubcontractingAndWiringTests
{
    // ========== SubcontractingManager Tests ==========

    private static SubcontractingOrder CreateSco()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id,
            Guid.NewGuid(), "FG Widget", 100m, 50m));
        return sco;
    }

    [Fact]
    public void CalculateRmConsumption_ProportionalRatio()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 50m);

        result.Length.ShouldBe(1);
        result[0].ConsumedQty.ShouldBe(100m); // 200 × (50/100) = 100
    }

    [Fact]
    public void CalculateRmConsumption_FullReceipt()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 100m);

        result[0].ConsumedQty.ShouldBe(200m); // Full consumption
    }

    [Fact]
    public void CalculateRmConsumption_MultipleRm()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Paint", 50m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 25m);

        result.Length.ShouldBe(2);
        result[0].ConsumedQty.ShouldBe(50m);  // 200 × (25/100)
        result[1].ConsumedQty.ShouldBe(12.5m); // 50 × (25/100)
    }

    [Fact]
    public void CalculateRmConsumption_ZeroFgQty_ReturnsEmpty()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        // No FG items → total qty is 0

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 10m);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateRmConsumption_ReturnReceipt_CalculatesNegativeConsumedQty()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        // When receiving return of -20 FG items against 100 ordered, RM should be -40 (returned to supplier)
        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: -20m, isReturn: true);

        result.Length.ShouldBe(1);
        result[0].ConsumedQty.ShouldBe(-40m); // 200 × (-20/100) = -40
    }

    [Fact]
    public void SubcontractingOrder_PerReceived_DefaultsZero()
    {
        var sco = CreateSco();
        sco.PerReceived.ShouldBe(0);
    }

    [Fact]
    public void SubcontractingOrder_PartialReceipt_TracksQty()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.Items.First().ReceivedQty = 40;

        var pending = sco.Items.First().Qty - sco.Items.First().ReceivedQty;
        pending.ShouldBe(60m);
    }

    // ========== SubcontractingRmConsumption DTO ==========

    [Fact]
    public void SubcontractingRmConsumption_StoresValues()
    {
        var c = new SubcontractingRmConsumption
        {
            ItemId = Guid.NewGuid(),
            RequiredQty = 200m,
            ConsumedQty = 100m,
            WarehouseId = Guid.NewGuid()
        };
        c.ConsumedQty.ShouldBe(100m);
        c.WarehouseId.ShouldNotBeNull();
    }

    // ========== SCO Status Transitions ==========

    [Fact]
    public void SubcontractingOrder_Submit_FromDraft()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);
    }

    [Fact]
    public void SubcontractingOrder_Close_FromOpen()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.MarkPartiallyReceived();
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    [Fact]
    public void SubcontractingOrder_Cancel_FromDraft()
    {
        var sco = CreateSco();
        sco.Cancel();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Cancelled);
    }

    [Fact]
    public void SubcontractingOrder_DoubleCancel_Throws()
    {
        var sco = CreateSco();
        sco.Cancel();
        Should.Throw<BusinessException>(() => sco.Cancel());
    }

    [Fact]
    public async Task ValidateReceiptAgainstOrderAsync_DifferentHeaderProject_Throws()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var mgr = new SubcontractingManager(scoRepo, scrRepo);

        var sco = CreateSco();
        sco.ProjectId = Guid.NewGuid();
        scoRepo.GetAsync(sco.Id).Returns(Task.FromResult(sco));

        var receipt = new SubcontractingReceipt(
            Guid.NewGuid(), sco.CompanyId, "SCR-001", DateTime.UtcNow, sco.SupplierId, sco.Id)
        {
            ProjectId = Guid.NewGuid() // Different project
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => mgr.ValidateReceiptAgainstOrderAsync(receipt));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Subcontracting Receipt Project cannot differ from Subcontracting Order Project");
    }

    [Fact]
    public async Task ValidateReceiptAgainstOrderAsync_DifferentItemProject_Throws()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var mgr = new SubcontractingManager(scoRepo, scrRepo);

        var projectId = Guid.NewGuid();
        var sco = CreateSco();
        sco.ProjectId = projectId;
        sco.Items.First().ProjectId = projectId;
        scoRepo.GetAsync(sco.Id).Returns(Task.FromResult(sco));

        var receipt = new SubcontractingReceipt(
            Guid.NewGuid(), sco.CompanyId, "SCR-001", DateTime.UtcNow, sco.SupplierId, sco.Id)
        {
            ProjectId = projectId
        };
        receipt.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), receipt.Id, sco.Items.First().ItemId, "FG Widget", 10m, 50m, Guid.NewGuid())); // Different item project

        var ex = await Should.ThrowAsync<BusinessException>(() => mgr.ValidateReceiptAgainstOrderAsync(receipt));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Subcontracting Receipt Item Project cannot differ from Subcontracting Order Item Project");
    }

    [Fact]
    public async Task ValidateReceiptAgainstOrderAsync_MatchingProjects_Passes()
    {
        var scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var mgr = new SubcontractingManager(scoRepo, scrRepo);

        var projectId = Guid.NewGuid();
        var sco = CreateSco();
        sco.ProjectId = projectId;
        sco.Items.First().ProjectId = projectId;
        scoRepo.GetAsync(sco.Id).Returns(Task.FromResult(sco));

        var receipt = new SubcontractingReceipt(
            Guid.NewGuid(), sco.CompanyId, "SCR-001", DateTime.UtcNow, sco.SupplierId, sco.Id)
        {
            ProjectId = projectId
        };
        receipt.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), receipt.Id, sco.Items.First().ItemId, "FG Widget", 10m, 50m, projectId));

        await mgr.ValidateReceiptAgainstOrderAsync(receipt);
    }

    // =========================================================================
    // ERPNext PR #59832 (commit 008fb0c12e): Subcontracting Order against Purchase Order Guards
    // =========================================================================

    [Fact]
    public void ValidateOrderAgainstPurchaseOrder_DraftPo_ThrowsInvalidStatusTransition()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "FG Part", 10m, 100m, 0m);

        var ex = Should.Throw<BusinessException>(() => mgr.ValidateOrderAgainstPurchaseOrder(po));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        ex.Data["detail"]!.ToString()!.ShouldContain("Please submit Purchase Order PO-001 before proceeding");
    }

    [Fact]
    public void ValidateOrderAgainstPurchaseOrder_CancelledPo_ThrowsInvalidStatusTransition()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "FG Part", 10m, 100m, 0m);
        po.Submit();
        po.Cancel();

        var ex = Should.Throw<BusinessException>(() => mgr.ValidateOrderAgainstPurchaseOrder(po));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void ValidateOrderAgainstPurchaseOrder_ClosedPo_ThrowsLinkedPurchaseOrderClosed()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-003", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "FG Part", 10m, 100m, 0m);
        po.Submit();
        po.Close();

        var ex = Should.Throw<BusinessException>(() => mgr.ValidateOrderAgainstPurchaseOrder(po));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed);
        ex.Data["detail"]!.ToString()!.ShouldContain("because it is Closed");
    }

    [Fact]
    public void ValidateOrderAgainstPurchaseOrder_FullyReceivedPo_ThrowsOverReceipt()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-004", DateTime.UtcNow);
        var poItem = po.AddItem(Guid.NewGuid(), "FG Part", 10m, 100m, 0m);
        po.Submit();
        poItem.ReceivedQty = 10m; // 100% received

        var ex = Should.Throw<BusinessException>(() => mgr.ValidateOrderAgainstPurchaseOrder(po));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.OverReceipt);
        ex.Data["detail"]!.ToString()!.ShouldContain("Cannot create more Subcontracting Orders against a fully received Purchase Order");
    }

    [Fact]
    public void ValidateOrderAgainstPurchaseOrder_ClosedItemRow_ThrowsLinkedPurchaseOrderClosed()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-005", DateTime.UtcNow);
        var itemId = Guid.NewGuid();
        var poItem = po.AddItem(itemId, "FG Part", 10m, 100m, 0m);
        po.Submit();
        poItem.IsClosed = true;

        var ex = Should.Throw<BusinessException>(() => mgr.ValidateOrderAgainstPurchaseOrder(po, new[] { itemId }));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Cannot create a Subcontracting Order for closed Purchase Order item");
    }

    [Fact]
    public void ValidateOrderAgainstPurchaseOrder_ValidSubmittedPo_Succeeds()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-006", DateTime.UtcNow);
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "FG Part", 10m, 100m, 0m);
        po.Submit();

        Should.NotThrow(() => mgr.ValidateOrderAgainstPurchaseOrder(po, new[] { itemId }));
    }

    // =========================================================================
    // ERPNext PR #59832: Drop-ship delivered quantity conversion from PO UOM to SO UOM
    // =========================================================================

    [Theory]
    [InlineData(10, 2, 1, 20)]    // 10 boxes of 2 = 20 units -> 20 single units in SO
    [InlineData(5, 1, 2, 2.5)]    // 5 units in PO (conversion 1) -> 2.5 pairs in SO (conversion 2)
    [InlineData(10, 1, 1, 10)]    // 1:1 conversion
    public void CalculateDropShipDeliveredQty_ConvertsCorrectly(
        decimal poReceivedQty, decimal poConversionFactor, decimal soConversionFactor, decimal expectedSoDeliveredQty)
    {
        var result = PurchaseOrderManager.CalculateDropShipDeliveredQty(
            poReceivedQty, poConversionFactor, soConversionFactor);
        result.ShouldBe(expectedSoDeliveredQty);
    }

    // =========================================================================
    // ERPNext PR #66400: Secondary item return qty running-total rounding
    // =========================================================================

    [Fact]
    public void CalculateReturnSecondaryQty_PartialReturnsAddUpToOriginalSecondaryQty()
    {
        // Original: 3 FG, 1 Secondary, precision 0 (e.g. integer UOM like Nos/Pcs)
        // Return 1 (1 FG): running 1/3 = 0.333 -> 0 (omitted)
        var r1 = SubcontractingManager.CalculateReturnSecondaryQty(
            originalFgQty: 3m, originalSecondaryQty: 1m,
            priorReturnedFgQty: 0m, currentReturnFgQty: 1m,
            priorReturnedSecondaryQty: 0m, precision: 0);
        r1.ShouldBe(0m);

        // Return 2 (1 FG): running 2/3 = 0.667 -> 1; delta = 1 - 0 = 1
        var r2 = SubcontractingManager.CalculateReturnSecondaryQty(
            originalFgQty: 3m, originalSecondaryQty: 1m,
            priorReturnedFgQty: 1m, currentReturnFgQty: 1m,
            priorReturnedSecondaryQty: 0m, precision: 0);
        r2.ShouldBe(1m);

        // Return 3 (last 1 FG): remaining = 1 - 1 = 0 (already fully returned)
        var r3 = SubcontractingManager.CalculateReturnSecondaryQty(
            originalFgQty: 3m, originalSecondaryQty: 1m,
            priorReturnedFgQty: 2m, currentReturnFgQty: 1m,
            priorReturnedSecondaryQty: 1m, precision: 0);
        r3.ShouldBe(0m);

        (r1 + r2 + r3).ShouldBe(1m);
    }

    [Fact]
    public void CalculateReturnSecondaryQty_LastReturnTakesExactRemainingRemainder()
    {
        // Original: 3 FG, 1 Secondary at precision 4: 1/3 = 0.3333
        // Two prior returns took 0.3333 each (0.6666 total); final return takes 0.3334 so total == 1.0000
        var last = SubcontractingManager.CalculateReturnSecondaryQty(
            originalFgQty: 3m, originalSecondaryQty: 1m,
            priorReturnedFgQty: 2m, currentReturnFgQty: 1m,
            priorReturnedSecondaryQty: 0.6666m, precision: 4);
        last.ShouldBe(0.3334m);
        (0.6666m + last).ShouldBe(1m);
    }

    [Fact]
    public void PickAvailableSerialNosForRow_SkipsSerialsHeldByEarlierSuppliedRows()
    {
        // Per ERPNext PR #66525: multiple supplied rows sharing rm_item_code must not pick overlapping serials
        var availablePool = new[] { "SN-001", "SN-002", "SN-003", "SN-004" };
        var existingRows = new[] { "SN-001\nSN-002", string.Empty };

        var usedSerials = SubcontractingManager.GetUsedSerialNos(existingRows);
        var pickedForRow2 = SubcontractingManager.PickAvailableSerialNosForRow(availablePool, usedSerials, requiredCount: 2);

        pickedForRow2.ShouldBe(new[] { "SN-003", "SN-004" });
        usedSerials.ShouldContain("SN-003");
        usedSerials.ShouldContain("SN-004");
    }

    [Fact]
    public void PickAvailableSerialNosForRow_CaseInsensitiveDeduplicationAndCommaMultiline()
    {
        var usedSerials = SubcontractingManager.GetUsedSerialNos(new[] { "sn-001, SN-002" });
        var availablePool = new[] { "SN-001", "sn-002", "SN-005" };

        var picked = SubcontractingManager.PickAvailableSerialNosForRow(availablePool, usedSerials, requiredCount: 2);

        picked.ShouldBe(new[] { "SN-005" });
    }
}
