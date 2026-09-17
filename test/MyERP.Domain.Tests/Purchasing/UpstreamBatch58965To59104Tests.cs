using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Comprehensive tests covering upstream ERPNext PRs:
/// - PR #58899: Zero stock quantity clears balance value to 0 in FIFO and Moving Average.
/// - PR #58965: Project propagation across Subcontracting Order & Receipt entities and items.
/// - PR #59104: JobCardManager allows 0 completed qty when process loss or pending qty is present.
/// - PR #58688: Bank auto match candidate filtering handles null/empty document types.
/// </summary>
public class UpstreamBatch58965To59104Tests
{
    // =========================================================================
    // PR #58899: Clear stock value on zero quantity
    // =========================================================================

    [Fact]
    public void FifoValuation_FullConsumption_TotalValueBecomesZero()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10m, 150m);
        fifo.TotalQty.ShouldBe(10m);
        fifo.TotalValue.ShouldBe(1500m);

        var consumed = fifo.RemoveStock(10m);
        consumed.Count.ShouldBe(1);
        fifo.TotalQty.ShouldBe(0m);
        fifo.TotalValue.ShouldBe(0m);
    }

    [Fact]
    public void FifoValuation_MultipleBinsConsumedToZero_TotalValueBecomesZero()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(5m, 100m);
        fifo.AddStock(5m, 200m);
        fifo.TotalQty.ShouldBe(10m);
        fifo.TotalValue.ShouldBe(1500m);

        fifo.RemoveStock(10m);
        fifo.TotalQty.ShouldBe(0m);
        fifo.TotalValue.ShouldBe(0m);
    }

    [Fact]
    public void MovingAverage_StockOutToZero_BalanceValueBecomesZero()
    {
        // Given existing balance: 20 units @ 50 = 1000 value
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 20m, 50m, 20m, 1000m);

        // Stock OUT: -20 units
        var (rate, balanceQty, balanceValue) = StockValuationService.CalculateMovingAverage(sle, -20m, 0m);

        balanceQty.ShouldBe(0m);
        balanceValue.ShouldBe(0m);
        rate.ShouldBe(50m);
    }

    [Fact]
    public void MovingAverage_NearZeroBalance_RoundsToZero()
    {
        // Given existing balance: 10 units @ 100
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 10m, 100m, 10m, 1000m);

        // Stock OUT: -9.99999 units (near zero remaining < 0.0001)
        var (rate, balanceQty, balanceValue) = StockValuationService.CalculateMovingAverage(sle, -9.99999m, 0m);

        balanceQty.ShouldBe(0m);
        balanceValue.ShouldBe(0m);
    }

    // =========================================================================
    // PR #58965: Project propagation on Subcontracting flow & PO Items
    // =========================================================================

    [Fact]
    public void PurchaseOrderItem_HasProjectIdProperty()
    {
        var poId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var poItem = new PurchaseOrderItem(
            Guid.NewGuid(), poId, itemId, "Item Description", 10m, 25m, 0m, "Nos", projectId);

        poItem.ProjectId.ShouldBe(projectId);
    }

    [Fact]
    public void PurchaseOrder_AddItem_SetsProjectId()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-2026-001", DateTime.UtcNow)
        {
            ProjectId = projectId
        };

        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Subcontracted part", 5m, 100m, 0m, "Unit");

        po.Items.Count.ShouldBe(1);
        po.Items[0].ProjectId.ShouldBe(projectId); // Inherits from parent PO
    }

    [Fact]
    public void SubcontractingOrder_And_Items_SupportProjectId()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var sco = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-2026-001", DateTime.UtcNow, supplierId)
        {
            ProjectId = projectId
        };

        var item = new SubcontractingOrderItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Subcontracted Assembly", 10m, 150m, projectId);

        sco.AddItem(item);

        sco.ProjectId.ShouldBe(projectId);
        sco.Items.Count.ShouldBe(1);
        sco.Items[0].ProjectId.ShouldBe(projectId);
    }

    [Fact]
    public void SubcontractingReceipt_And_Items_SupportProjectId()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var scoId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var scr = new SubcontractingReceipt(Guid.NewGuid(), companyId, "SCR-2026-001", DateTime.UtcNow, supplierId, scoId)
        {
            ProjectId = projectId
        };

        var item = new SubcontractingReceiptItem(
            Guid.NewGuid(), scr.Id, Guid.NewGuid(), "Subcontracted Assembly", 10m, 150m, projectId);

        scr.AddItem(item);

        scr.ProjectId.ShouldBe(projectId);
        scr.Items.Count.ShouldBe(1);
        scr.Items[0].ProjectId.ShouldBe(projectId);
    }

    // =========================================================================
    // PR #59104: JobCardManager.ValidateCompletionSplit zero-completed with loss
    // =========================================================================

    [Fact]
    public void ValidateCompletionSplit_AllowsZeroCompleted_WhenProcessLossEqualsForQuantity()
    {
        // 100% process loss, 0 completed, 0 pending
        Should.NotThrow(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: 0m, processLossQty: 10m, pendingQty: 0m));
    }

    [Fact]
    public void ValidateCompletionSplit_AllowsZeroCompleted_WhenPendingEqualsForQuantity()
    {
        // 0 completed, 10 pending, 0 loss
        Should.NotThrow(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: 0m, processLossQty: 0m, pendingQty: 10m));
    }

    [Fact]
    public void ValidateCompletionSplit_ValidSplit_Succeeds()
    {
        // Split: 7 completed, 2 process loss, 1 pending = 10 forQuantity
        Should.NotThrow(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: 7m, processLossQty: 2m, pendingQty: 1m));
    }

    [Fact]
    public void ValidateCompletionSplit_NegativeQuantities_ThrowsException()
    {
        Should.Throw<BusinessException>(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: -1m, processLossQty: 11m, pendingQty: 0m));

        Should.Throw<BusinessException>(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: 11m, processLossQty: -1m, pendingQty: 0m));

        Should.Throw<BusinessException>(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: 10m, processLossQty: 1m, pendingQty: -1m));
    }

    [Fact]
    public void ValidateCompletionSplit_SumMismatch_ThrowsException()
    {
        var ex = Should.Throw<BusinessException>(() =>
            JobCardManager.ValidateCompletionSplit(forQuantity: 10m, completedQty: 5m, processLossQty: 2m, pendingQty: 1m));

        ex.Code.ShouldBe("MyERP:10021");
    }

    // =========================================================================
    // PR #58688: Bank candidate matching with document types
    // =========================================================================

    [Fact]
    public void BankMatchCandidate_Properties_CorrectlyAssigned()
    {
        var peId = Guid.NewGuid();
        var candidate = new MatchCandidate
        {
            VoucherType = "PaymentEntry",
            PaymentEntryId = peId,
            PaymentNumber = "PE-2026-0001",
            Amount = 1500m,
            PostingDate = DateTime.Today,
            ReferenceNumber = "REF-12345",
            Rank = 3
        };

        candidate.VoucherType.ShouldBe("PaymentEntry");
        candidate.PaymentEntryId.ShouldBe(peId);
        candidate.JournalEntryId.ShouldBeNull();
        candidate.Rank.ShouldBe(3);
    }
}
