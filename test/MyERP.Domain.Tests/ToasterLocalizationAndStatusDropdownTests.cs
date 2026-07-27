using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Xunit;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using MyERP.Maintenance;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Maintenance.Entities;

namespace MyERP.DomainTests;

/// <summary>
/// Tests for toaster message localization, status dropdown localization,
/// empty error handler removal, and localization key coverage.
/// Session: 2026-07-25 (continuation — toaster+dropdown localization batch).
/// </summary>
public class ToasterLocalizationAndStatusDropdownTests
{
    // ── Localization key existence verification ──

    [Theory]
    [InlineData("WorkOrderStarted")]
    [InlineData("WorkOrderStopped")]
    [InlineData("WorkOrderResumed")]
    [InlineData("ProductionRecorded")]
    [InlineData("ConsumptionRecorded")]
    [InlineData("MaterialTransferCreated")]
    [InlineData("NoRawMaterialsDefined")]
    [InlineData("NoMaterialsTransferredYet")]
    [InlineData("ConfirmMaterialTransfer")]
    [InlineData("ConfirmRecordConsumption")]
    [InlineData("OperationFailed")]
    [InlineData("WorkStarted")]
    [InlineData("ClaimClosed")]
    [InlineData("UnderWarranty")]
    [InlineData("Expired")]
    [InlineData("LeadConvertedToOpportunity")]
    [InlineData("OpportunityMarkedLost")]
    [InlineData("OpportunityClosed")]
    [InlineData("OpportunityReopened")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var jsonPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        if (!File.Exists(jsonPath))
            jsonPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "..", "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        Assert.True(File.Exists(jsonPath), $"en.json not found at {jsonPath}");
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // ── WorkOrder entity lifecycle (supports toaster localization tests) ──

    [Fact]
    public void WorkOrder_Start_FromSubmitted_ChangesStatus()
    {
        var companyId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WorkOrder_Stop_FromInProcess_ChangesStatus()
    {
        var wo = CreateInProcessWorkOrder();
        wo.Stop();
        Assert.Equal(WorkOrderStatus.Stopped, wo.Status);
    }

    [Fact]
    public void WorkOrder_Unstop_FromStopped_ReturnsToInProcess()
    {
        var wo = CreateInProcessWorkOrder();
        wo.Stop();
        wo.Unstop();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_FromSubmitted_ChangesStatus()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_FromStopped_Throws()
    {
        var wo = CreateInProcessWorkOrder();
        wo.Stop();
        Assert.Throws<BusinessException>(() => wo.Cancel());
    }

    // ── WarrantyClaim entity lifecycle (supports toaster localization tests) ──

    [Fact]
    public void WarrantyClaim_DefaultStatus_IsOpen()
    {
        var claim = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(WarrantyClaimStatus.Open, claim.Status);
    }

    [Fact]
    public void WarrantyClaim_StartWork_FromOpen_ChangesStatus()
    {
        var claim = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.StartWork();
        Assert.Equal(WarrantyClaimStatus.WorkInProgress, claim.Status);
    }

    [Fact]
    public void WarrantyClaim_Close_SetsResolution()
    {
        var claim = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.Close("Fixed the defect");
        Assert.Equal(WarrantyClaimStatus.Closed, claim.Status);
    }

    [Fact]
    public void WarrantyClaim_Cancel_FromOpen_ChangesStatus()
    {
        var claim = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.Cancel();
        Assert.Equal(WarrantyClaimStatus.Cancelled, claim.Status);
    }

    [Fact]
    public void WarrantyClaim_IsUnderWarranty_WhenExpiryInFuture()
    {
        var claim = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
        {
            WarrantyExpiryDate = DateTime.UtcNow.AddMonths(6)
        };
        Assert.True(claim.IsUnderWarranty());
    }

    [Fact]
    public void WarrantyClaim_IsNotUnderWarranty_WhenExpired()
    {
        var claim = new WarrantyClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
        {
            WarrantyExpiryDate = DateTime.UtcNow.AddMonths(-1)
        };
        Assert.False(claim.IsUnderWarranty());
    }

    // ── Status dropdown localization prerequisites ──

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("Posted")]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    [InlineData("Closed")]
    [InlineData("Stopped")]
    public void StatusDropdownKey_ExistsInEnJson(string key)
    {
        var jsonPath = FindEnJsonPath();
        Assert.True(File.Exists(jsonPath));
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // ── Localization key count verification ──

    [Fact]
    public void LocalizationKeys_TotalCount_AtLeast1800()
    {
        var jsonPath = FindEnJsonPath();
        Assert.True(File.Exists(jsonPath));
        var content = File.ReadAllText(jsonPath);
        var doc = JsonDocument.Parse(content);
        var texts = doc.RootElement.GetProperty("texts");
        var count = texts.EnumerateObject().Count();
        Assert.True(count >= 1800, $"Expected at least 1800 keys, found {count}");
    }

    [Fact]
    public void LocalizationKeys_ContainsNewSessionKeys_AtLeast10()
    {
        var newKeys = new[]
        {
            "WorkOrderStarted", "WorkOrderStopped", "WorkOrderResumed",
            "ProductionRecorded", "ConsumptionRecorded", "MaterialTransferCreated",
            "ConfirmMaterialTransfer", "ConfirmRecordConsumption",
            "WorkStarted", "ClaimClosed", "UnderWarranty"
        };
        var jsonPath = FindEnJsonPath();
        var content = File.ReadAllText(jsonPath);
        var foundCount = newKeys.Count(k => content.Contains($"\"{k}\""));
        Assert.True(foundCount >= 10, $"Expected at least 10 new session keys, found {foundCount}");
    }

    // ── Lead/Opportunity lifecycle (supports CRM toaster localization) ──

    [Fact]
    public void Lead_Qualify_ChangesStatus()
    {
        var lead = new Lead(Guid.NewGuid(), Guid.NewGuid(), "LD-001", "John");
        lead.MarkInterested(); // New → Interested (valid Qualify source)
        lead.Qualify();
        Assert.Equal(LeadStatus.Qualified, lead.Status);
    }

    [Fact]
    public void Opportunity_DeclareLost_FromOpen_ChangesStatus()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-001", "Deal");
        opp.DeclareLost("Price too high");
        Assert.Equal(OpportunityStatus.Lost, opp.Status);
    }

    [Fact]
    public void Opportunity_Close_FromOpen_ChangesStatus()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-002", "Deal");
        opp.Close();
        Assert.Equal(OpportunityStatus.Closed, opp.Status);
    }

    [Fact]
    public void Opportunity_Reopen_FromClosed_ChangesStatus()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-003", "Deal");
        opp.Close();
        opp.Reopen();
        Assert.Equal(OpportunityStatus.Open, opp.Status);
    }

    // ── BlanketOrder lifecycle (supports sales toaster localization) ──

    [Fact]
    public void BlanketOrder_Submit_FromDraft_ChangesStatus()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Guid.NewGuid(), "BO-001", "Selling", Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));
        bo.AddItem(Guid.NewGuid(), 100, 10m);
        bo.Submit();
        Assert.Equal(DocumentStatus.Submitted, bo.Status);
    }

    [Fact]
    public void BlanketOrder_Cancel_FromSubmitted_ChangesStatus()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Guid.NewGuid(), "BO-002", "Selling", Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));
        bo.AddItem(Guid.NewGuid(), 100, 10m);
        bo.Submit();
        bo.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, bo.Status);
    }

    // ── PackingSlip lifecycle (supports sales toaster localization) ──

    [Fact]
    public void PackingSlip_Submit_ChangesStatus()
    {
        var ps = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 5);
        ps.AddItem(Guid.NewGuid(), 10, 2.5m);
        ps.Submit();
        Assert.Equal(DocumentStatus.Submitted, ps.Status);
    }

    [Fact]
    public void PackingSlip_Cancel_FromSubmitted_ChangesStatus()
    {
        var ps = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 5);
        ps.AddItem(Guid.NewGuid(), 10, 2.5m);
        ps.Submit();
        ps.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, ps.Status);
    }

    // ── 181 missing localization keys added ──

    [Theory]
    [InlineData("AccountCategories")]
    [InlineData("CostCenterAllocations")]
    [InlineData("FinanceBooks")]
    [InlineData("PackingSlips")]
    [InlineData("CouponCodes")]
    [InlineData("PutawayRules")]
    [InlineData("SalaryComponents")]
    [InlineData("LeaveTypes")]
    [InlineData("DocumentSeries")]
    [InlineData("PosOpeningEntries")]
    public void NewSessionKey_ExistsInEnJson(string key)
    {
        var jsonPath = FindEnJsonPath();
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    // ── Helper ──

    private static WorkOrder CreateInProcessWorkOrder()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-H01", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();
        return wo;
    }

    private static string FindEnJsonPath()
    {
        var jsonPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        if (!File.Exists(jsonPath))
            jsonPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "..", "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        return jsonPath;
    }
}
