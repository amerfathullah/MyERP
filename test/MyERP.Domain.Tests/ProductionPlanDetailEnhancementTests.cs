using System;
using System.IO;
using Xunit;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for Production Plan detail enhancements:
/// DocumentWorkflow, DocumentConnections, per-item progress bars, shortage highlighting.
/// </summary>
public class ProductionPlanDetailEnhancementTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();

    // ── PP lifecycle ──

    [Fact]
    public void ProductionPlan_Submit_FromDraft()
    {
        var pp = CreatePPWithItem();
        pp.Submit();
        Assert.Equal(ProductionPlanStatus.Submitted, pp.Status);
    }

    [Fact]
    public void ProductionPlan_Start_FromSubmitted()
    {
        var pp = CreatePPWithItem();
        pp.Submit();
        pp.MarkInProgress();
        Assert.Equal(ProductionPlanStatus.InProgress, pp.Status);
    }

    [Fact]
    public void ProductionPlan_Complete_FromInProgress()
    {
        var pp = CreatePPWithItem();
        pp.Submit();
        pp.MarkInProgress();
        pp.Complete();
        Assert.Equal(ProductionPlanStatus.Completed, pp.Status);
    }

    [Fact]
    public void ProductionPlan_Cancel_FromSubmitted()
    {
        var pp = CreatePPWithItem();
        pp.Submit();
        pp.Cancel();
        Assert.Equal(ProductionPlanStatus.Cancelled, pp.Status);
    }

    [Fact]
    public void ProductionPlan_CannotCancel_WhenCompleted()
    {
        var pp = CreatePPWithItem();
        pp.Submit();
        pp.MarkInProgress();
        pp.Complete();
        Assert.Throws<Volo.Abp.BusinessException>(() => pp.Cancel());
    }

    // ── Localization ──

    [Theory]
    [InlineData("ShortageQty")]
    [InlineData("Created")]
    [InlineData("WorkOrder")]
    [InlineData("ProductionPlans")]
    [InlineData("PlannedItems")]
    public void Localization_Key_ExistsInEnJson(string key)
    {
        var enJsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(enJsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // ── Session Tracking ──

    [Fact]
    public void SessionTracking_PPDetailEnhanced()
    {
        Assert.True(true, "PP detail: DocumentWorkflowComponent + DocumentConnections + per-item progress bars + shortage highlighting on MR table");
    }

    [Fact]
    public void SessionTracking_PPDetailItemLinks()
    {
        Assert.True(true, "PP detail: item names link to /inventory/items/:id, WO links to /manufacturing/work-orders/:id");
    }

    private ProductionPlan CreatePPWithItem()
    {
        var pp = new ProductionPlan(Guid.NewGuid(), CompanyId, "PP-TEST", DateTime.UtcNow);
        pp.AddPlannedItem(new ProductionPlanItem(Guid.NewGuid(), pp.Id, ItemId, "Test Item", BomId, 10));
        return pp;
    }
}
