using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Inventory.BackgroundJobs;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for the enhanced Repost Item Valuation pipeline:
/// - RepostItemValuation entity lifecycle (Queued → InProgress → Completed/Failed/Skipped)
/// - GlRepostService voucher type validation + batch processing
/// - RepostItemValuationArgs with tracking ID
/// - Dedup coverage detection
/// </summary>
public class RepostPipelineTests
{
    [Fact]
    public void RepostItemValuation_DefaultStatus_IsQueued()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(RepostStatus.Queued, riv.Status);
    }

    [Fact]
    public void RepostItemValuation_StartProcessing_TransitionsToInProgress()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());

        riv.StartProcessing();

        Assert.Equal(RepostStatus.InProgress, riv.Status);
    }

    [Fact]
    public void RepostItemValuation_StartProcessing_FromNonQueued_Throws()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing(); // Now InProgress

        Assert.Throws<Volo.Abp.BusinessException>(() => riv.StartProcessing());
    }

    [Fact]
    public void RepostItemValuation_Complete_SetsStatusAndCount()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing();

        riv.Complete(42);

        Assert.Equal(RepostStatus.Completed, riv.Status);
        Assert.Equal(42, riv.TotalAffectedEntries);
        Assert.Equal(42, riv.CurrentIndex);
    }

    [Fact]
    public void RepostItemValuation_Fail_SetsStatusAndError()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing();

        riv.Fail("Database timeout");

        Assert.Equal(RepostStatus.Failed, riv.Status);
        Assert.Equal("Database timeout", riv.ErrorLog);
    }

    [Fact]
    public void RepostItemValuation_MarkSkipped_SetsStatusAndReason()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());

        riv.MarkSkipped("Covered by broader repost");

        Assert.Equal(RepostStatus.Skipped, riv.Status);
        Assert.Equal("Covered by broader repost", riv.ErrorLog);
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_EntireCompany_Covers_ItemWise()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var broader = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.EntireCompany, new DateTime(2026, 1, 1));
        var narrower = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, new DateTime(2026, 3, 1), itemId, warehouseId);

        Assert.True(narrower.IsCoveredBy(broader));
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_SameItemWarehouse_EarlierDate_Covers()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var earlier = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, new DateTime(2026, 1, 1), itemId, warehouseId);
        var later = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, new DateTime(2026, 3, 1), itemId, warehouseId);

        Assert.True(later.IsCoveredBy(earlier));
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_CompletedRepost_DoesNotCover()
    {
        var companyId = Guid.NewGuid();
        var broader = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.EntireCompany, new DateTime(2026, 1, 1));
        broader.StartProcessing();
        broader.Complete(100); // Now completed

        var narrower = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, new DateTime(2026, 3, 1), Guid.NewGuid(), Guid.NewGuid());

        Assert.False(narrower.IsCoveredBy(broader));
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_DifferentItem_DoesNotCover()
    {
        var companyId = Guid.NewGuid();

        var riv1 = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());
        var riv2 = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(riv1.IsCoveredBy(riv2));
    }

    // --- GlRepostService ---

    [Fact]
    public void GlRepostService_IsRepostAllowed_ValidTypes_ReturnsTrue()
    {
        Assert.True(GlRepostService.IsRepostAllowed("SalesInvoice"));
        Assert.True(GlRepostService.IsRepostAllowed("PurchaseInvoice"));
        Assert.True(GlRepostService.IsRepostAllowed("PaymentEntry"));
        Assert.True(GlRepostService.IsRepostAllowed("JournalEntry"));
        Assert.True(GlRepostService.IsRepostAllowed("PurchaseReceipt"));
        Assert.True(GlRepostService.IsRepostAllowed("DeliveryNote"));
        Assert.True(GlRepostService.IsRepostAllowed("StockEntry"));
    }

    [Fact]
    public void GlRepostService_IsRepostAllowed_InvalidTypes_ReturnsFalse()
    {
        Assert.False(GlRepostService.IsRepostAllowed("SalesOrder"));
        Assert.False(GlRepostService.IsRepostAllowed("PurchaseOrder"));
        Assert.False(GlRepostService.IsRepostAllowed("MaterialRequest"));
        Assert.False(GlRepostService.IsRepostAllowed("Quotation"));
        Assert.False(GlRepostService.IsRepostAllowed(""));
        Assert.False(GlRepostService.IsRepostAllowed("Unknown"));
    }

    [Fact]
    public void GlRepostService_IsRepostAllowed_CaseInsensitive()
    {
        Assert.True(GlRepostService.IsRepostAllowed("salesinvoice"));
        Assert.True(GlRepostService.IsRepostAllowed("PURCHASERECEIPT"));
        Assert.True(GlRepostService.IsRepostAllowed("deliveryNote"));
    }

    // --- GlRepostResult ---

    [Fact]
    public void GlRepostResult_TotalProcessed_SumsAllCategories()
    {
        var result = new GlRepostResult(5, 2, 1, new List<string> { "error1" });

        Assert.Equal(8, result.TotalProcessed);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void GlRepostResult_NoErrors_HasErrorsFalse()
    {
        var result = new GlRepostResult(10, 0, 0, new List<string>());

        Assert.Equal(10, result.TotalProcessed);
        Assert.False(result.HasErrors);
    }

    // --- RepostItemValuationArgs ---

    [Fact]
    public void RepostItemValuationArgs_RepostId_DefaultsNull()
    {
        var args = new RepostItemValuationArgs();

        Assert.Null(args.RepostId);
    }

    [Fact]
    public void RepostItemValuationArgs_RepostId_CanBeSet()
    {
        var repostId = Guid.NewGuid();
        var args = new RepostItemValuationArgs { RepostId = repostId };

        Assert.Equal(repostId, args.RepostId);
    }

    [Fact]
    public void RepostItemValuation_RepostGlEntries_DefaultsTrue()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.Today, Guid.NewGuid(), Guid.NewGuid());

        Assert.True(riv.RepostGlEntries);
    }

    [Fact]
    public void RepostItemValuation_EntireCompany_NoItemWarehouse()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.EntireCompany, DateTime.Today);

        Assert.Null(riv.ItemId);
        Assert.Null(riv.WarehouseId);
        Assert.Equal(RepostMethod.EntireCompany, riv.BasedOn);
    }
}
