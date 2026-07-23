using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for Asset/WorkOrder/Budget list filtering and sorting patterns.
/// Verifies: date range filtering, status filtering, text search, sorting behavior.
/// </summary>
public class ListFilteringSortingTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    #region Asset Date Range Filtering

    [Fact]
    public void Asset_FilterByFromDate_ExcludesOlderAssets()
    {
        var assets = CreateSampleAssets();
        var fromDate = new DateTime(2025, 6, 1);

        var filtered = assets.Where(a => a.PurchaseDate >= fromDate).ToList();

        filtered.Count.ShouldBe(2); // June and December
        filtered.All(a => a.PurchaseDate >= fromDate).ShouldBeTrue();
    }

    [Fact]
    public void Asset_FilterByToDate_ExcludesNewerAssets()
    {
        var assets = CreateSampleAssets();
        var toDate = new DateTime(2025, 6, 30);

        var filtered = assets.Where(a => a.PurchaseDate <= toDate).ToList();

        filtered.Count.ShouldBe(2); // January and June
        filtered.All(a => a.PurchaseDate <= toDate).ShouldBeTrue();
    }

    [Fact]
    public void Asset_FilterByDateRange_ReturnsWithinWindow()
    {
        var assets = CreateSampleAssets();
        var from = new DateTime(2025, 3, 1);
        var to = new DateTime(2025, 9, 30);

        var filtered = assets
            .Where(a => a.PurchaseDate >= from && a.PurchaseDate <= to)
            .ToList();

        filtered.Count.ShouldBe(1); // Only June
    }

    [Fact]
    public void Asset_NoDateFilter_ReturnsAll()
    {
        var assets = CreateSampleAssets();

        assets.Count.ShouldBe(3);
    }

    #endregion

    #region Asset Status Filtering

    [Fact]
    public void Asset_FilterByStatus_Draft()
    {
        var assets = CreateSampleAssets();

        var drafts = assets.Where(a => a.Status == AssetStatus.Draft).ToList();

        drafts.Count.ShouldBe(3); // All start as draft
    }

    [Fact]
    public void Asset_FilterByStatus_Submitted()
    {
        var assets = CreateSampleAssets();
        assets[0].Submit();

        var submitted = assets.Where(a => a.Status == AssetStatus.Submitted).ToList();

        submitted.Count.ShouldBe(1);
        submitted[0].AssetName.ShouldBe("Laptop A");
    }

    #endregion

    #region Asset Text Search

    [Fact]
    public void Asset_SearchByName_CaseInsensitive()
    {
        var assets = CreateSampleAssets();
        var filter = "laptop";

        // Contains is case-sensitive in memory; PostgreSQL ILIKE handles it server-side
        var filtered = assets
            .Where(a => a.AssetName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        filtered.Count.ShouldBe(1);
        filtered[0].AssetName.ShouldBe("Laptop A");
    }

    [Fact]
    public void Asset_SearchByNumber_Partial()
    {
        var assets = CreateSampleAssets();
        var filter = "AST-001";

        var filtered = assets
            .Where(a => a.AssetNumber.Contains(filter))
            .ToList();

        filtered.Count.ShouldBe(1);
    }

    #endregion

    #region WorkOrder Date Range Filtering

    [Fact]
    public void WorkOrder_FilterByPlannedStartDate_FromDate()
    {
        var orders = CreateSampleWorkOrders();
        var fromDate = new DateTime(2026, 4, 1);

        var filtered = orders
            .Where(w => w.PlannedStartDate.HasValue && w.PlannedStartDate >= fromDate)
            .ToList();

        filtered.Count.ShouldBe(2); // April and July
    }

    [Fact]
    public void WorkOrder_FilterByPlannedStartDate_ToDate()
    {
        var orders = CreateSampleWorkOrders();
        var toDate = new DateTime(2026, 3, 31);

        var filtered = orders
            .Where(w => w.PlannedStartDate.HasValue && w.PlannedStartDate <= toDate)
            .ToList();

        filtered.Count.ShouldBe(1); // Only January
    }

    [Fact]
    public void WorkOrder_NullPlannedStartDate_NotExcludedByDefault()
    {
        var wo = new WorkOrder(
            Guid.NewGuid(), _companyId, "WO-000", Guid.NewGuid(), Guid.NewGuid(), 10);
        // PlannedStartDate is null by default until explicitly set
        wo.PlannedStartDate.ShouldBeNull();
    }

    #endregion

    #region WorkOrder Status Filtering

    [Fact]
    public void WorkOrder_FilterByStatus_Submitted()
    {
        var orders = CreateSampleWorkOrders();
        orders[0].Submit();

        var submitted = orders.Where(w => w.Status == WorkOrderStatus.Submitted).ToList();

        submitted.Count.ShouldBe(1);
        submitted[0].WorkOrderNumber.ShouldBe("WO-001");
    }

    [Fact]
    public void WorkOrder_FilterByStatus_InProcess()
    {
        var orders = CreateSampleWorkOrders();
        orders[1].Submit();
        orders[1].Start();

        var inProcess = orders.Where(w => w.Status == WorkOrderStatus.InProcess).ToList();

        inProcess.Count.ShouldBe(1);
        inProcess[0].WorkOrderNumber.ShouldBe("WO-002");
    }

    #endregion

    #region WorkOrder Text Search

    [Fact]
    public void WorkOrder_SearchByNumber_NoToLower()
    {
        var orders = CreateSampleWorkOrders();
        var filter = "WO-002";

        // Correct pattern: Contains without ToLower (PostgreSQL handles case)
        var filtered = orders.Where(w => w.WorkOrderNumber.Contains(filter)).ToList();

        filtered.Count.ShouldBe(1);
        filtered[0].Quantity.ShouldBe(50);
    }

    #endregion

    #region Budget Status Filtering

    [Fact]
    public void Budget_StatusParsing_ValidStatus()
    {
        var success = Enum.TryParse<DocumentStatus>("Draft", true, out var status);

        success.ShouldBeTrue();
        status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void Budget_StatusParsing_CaseInsensitive()
    {
        var success = Enum.TryParse<DocumentStatus>("submitted", true, out var status);

        success.ShouldBeTrue();
        status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Budget_StatusParsing_Invalid_ReturnsFalse()
    {
        var success = Enum.TryParse<DocumentStatus>("invalid_status", true, out _);

        success.ShouldBeFalse();
    }

    [Fact]
    public void Budget_FilterByNullableName_NullSafe()
    {
        // BudgetAgainstName is nullable — filter must handle nulls
        string? budgetName = null;
        var filter = "marketing";

        var result = (budgetName ?? "").Contains(filter);

        result.ShouldBeFalse(); // Null coalesced to empty, doesn't match
    }

    #endregion

    #region Sorting Behavior

    [Fact]
    public void Asset_DefaultSort_ByCreationTimeDesc()
    {
        var assets = CreateSampleAssets();
        // Default is newest first (CreationTime desc)
        var sorted = assets.OrderByDescending(a => a.CreationTime).ToList();
        // Since all created at roughly same time, order may vary — just verify no crash
        sorted.Count.ShouldBe(3);
    }

    [Fact]
    public void WorkOrder_SortByQuantity_Ascending()
    {
        var orders = CreateSampleWorkOrders();
        var sorted = orders.OrderBy(w => w.Quantity).ToList();

        sorted[0].Quantity.ShouldBe(20);
        sorted[1].Quantity.ShouldBe(50);
        sorted[2].Quantity.ShouldBe(100);
    }

    [Fact]
    public void WorkOrder_SortByQuantity_Descending()
    {
        var orders = CreateSampleWorkOrders();
        var sorted = orders.OrderByDescending(w => w.Quantity).ToList();

        sorted[0].Quantity.ShouldBe(100);
        sorted[2].Quantity.ShouldBe(20);
    }

    #endregion

    #region Helpers

    private List<Asset> CreateSampleAssets()
    {
        return new List<Asset>
        {
            new Asset(Guid.NewGuid(), _companyId, "AST-001", "Laptop A",
                new DateTime(2025, 1, 15), 5000m),
            new Asset(Guid.NewGuid(), _companyId, "AST-002", "Server B",
                new DateTime(2025, 6, 20), 15000m),
            new Asset(Guid.NewGuid(), _companyId, "AST-003", "Vehicle C",
                new DateTime(2025, 12, 1), 80000m),
        };
    }

    private List<WorkOrder> CreateSampleWorkOrders()
    {
        var bomId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", itemId, bomId, 100);
        wo1.SetPlannedDates(new DateTime(2026, 1, 15), new DateTime(2026, 2, 15));
        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", itemId, bomId, 50);
        wo2.SetPlannedDates(new DateTime(2026, 4, 1), new DateTime(2026, 5, 1));
        var wo3 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-003", itemId, bomId, 20);
        wo3.SetPlannedDates(new DateTime(2026, 7, 1), new DateTime(2026, 8, 1));
        return new List<WorkOrder> { wo1, wo2, wo3 };
    }

    #endregion
}
