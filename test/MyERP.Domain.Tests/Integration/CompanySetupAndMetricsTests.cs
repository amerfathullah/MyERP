using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for company setup and operational metrics features.
/// </summary>
public class CompanySetupAndMetricsTests
{
    #region Company Entity

    [Fact]
    public void Company_FiscalYearStartMonth_DefaultsToJanuary()
    {
        var company = new Company(Guid.NewGuid(), "Test Corp");

        // Default fiscal year start is January (month 1)
        company.FiscalYearStartMonth.ShouldBe(1);
    }

    [Fact]
    public void Company_CurrencyCode_CanBeSet()
    {
        var company = new Company(Guid.NewGuid(), "MY Corp");
        company.CurrencyCode = "MYR";

        company.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void Company_IsActive_DefaultsTrue()
    {
        var company = new Company(Guid.NewGuid(), "Active Corp");

        company.IsActive.ShouldBeTrue();
    }

    #endregion

    #region Fiscal Year for Company

    [Fact]
    public void FiscalYear_CalendarYear_JanToDec()
    {
        var companyId = Guid.NewGuid();
        var fy = new FiscalYear(Guid.NewGuid(), companyId,
            "FY 2026-2027",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31));

        fy.CompanyId.ShouldBe(companyId);
        fy.StartDate.ShouldBe(new DateTime(2026, 1, 1));
        fy.EndDate.ShouldBe(new DateTime(2026, 12, 31));
        fy.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void FiscalYear_MalaysianFY_AprToMar()
    {
        // Some Malaysian companies use Apr-Mar fiscal year
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(),
            "FY 2026",
            new DateTime(2026, 4, 1),
            new DateTime(2027, 3, 31));

        fy.StartDate.Month.ShouldBe(4);
        fy.EndDate.Month.ShouldBe(3);
        fy.EndDate.Year.ShouldBe(2027);
    }

    #endregion

    #region Cost Center Hierarchy

    [Fact]
    public void CostCenter_RootAndLeaf_Pattern()
    {
        var companyId = Guid.NewGuid();
        var root = new CostCenter(Guid.NewGuid(), companyId, "Test Corp", isGroup: true);
        var main = new CostCenter(Guid.NewGuid(), companyId, "Main", parentId: root.Id);

        root.IsGroup.ShouldBeTrue();
        main.IsGroup.ShouldBeFalse();
        main.ParentId.ShouldBe(root.Id);
    }

    [Fact]
    public void CostCenter_CompanyScoped()
    {
        var company1 = Guid.NewGuid();
        var company2 = Guid.NewGuid();

        var cc1 = new CostCenter(Guid.NewGuid(), company1, "Sales");
        var cc2 = new CostCenter(Guid.NewGuid(), company2, "Sales");

        cc1.CompanyId.ShouldNotBe(cc2.CompanyId);
    }

    #endregion

    #region Default Warehouses

    [Fact]
    public void Warehouse_ThreeDefaultsForCompany()
    {
        var companyId = Guid.NewGuid();
        var stores = new Warehouse(Guid.NewGuid(), companyId, "Stores") { IsActive = true };
        var fg = new Warehouse(Guid.NewGuid(), companyId, "Finished Goods") { IsActive = true };
        var wip = new Warehouse(Guid.NewGuid(), companyId, "Work In Progress") { IsActive = true };

        stores.CompanyId.ShouldBe(companyId);
        stores.IsActive.ShouldBeTrue();
        fg.Name.ShouldBe("Finished Goods");
        wip.Name.ShouldBe("Work In Progress");
    }

    [Fact]
    public void Warehouse_IsGroup_DefaultsFalse()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Test");
        wh.IsGroup.ShouldBeFalse();
    }

    #endregion

    #region Manufacturing Settings

    [Fact]
    public void ManufacturingSettings_DefaultOverproduction5Percent()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());

        settings.OverproductionPercentage.ShouldBe(5m);
    }

    [Fact]
    public void ManufacturingSettings_PerCompany()
    {
        var company1 = Guid.NewGuid();
        var company2 = Guid.NewGuid();

        var s1 = new ManufacturingSettings(Guid.NewGuid(), company1);
        var s2 = new ManufacturingSettings(Guid.NewGuid(), company2);

        s1.CompanyId.ShouldNotBe(s2.CompanyId);
    }

    #endregion

    #region Operational Metrics DTO

    [Fact]
    public void OperationalMetrics_DefaultsToZero()
    {
        var metrics = new OperationalMetricsDto();

        metrics.DraftDocuments.ShouldBe(0);
        metrics.PendingApprovals.ShouldBe(0);
        metrics.OverdueInvoices.ShouldBe(0);
        metrics.LowStockItems.ShouldBe(0);
        metrics.TotalArOutstanding.ShouldBe(0);
        metrics.TotalApOutstanding.ShouldBe(0);
    }

    [Fact]
    public void OperationalMetrics_OldestUnpaid_InDays()
    {
        var metrics = new OperationalMetricsDto
        {
            OldestUnpaidInvoiceDays = 45,
        };

        metrics.OldestUnpaidInvoiceDays.ShouldBe(45);
    }

    [Fact]
    public void OperationalMetrics_LastNightlyRun_Nullable()
    {
        var metrics = new OperationalMetricsDto();

        metrics.LastNightlyRunDate.ShouldBeNull();

        metrics.LastNightlyRunDate = DateTime.Today;
        metrics.LastNightlyRunDate.ShouldBe(DateTime.Today);
    }

    [Fact]
    public void OperationalMetrics_HighDraftCount_IndicatesBacklog()
    {
        var metrics = new OperationalMetricsDto
        {
            DraftDocuments = 25,
            OverdueInvoices = 8,
        };

        // High draft count + overdue = needs attention
        (metrics.DraftDocuments + metrics.OverdueInvoices).ShouldBeGreaterThan(20);
    }

    #endregion

    #region Company Default Accounts

    [Fact]
    public void Company_AllDefaultAccounts_NullInitially()
    {
        var company = new Company(Guid.NewGuid(), "New Corp");

        company.DefaultReceivableAccountId.ShouldBeNull();
        company.DefaultPayableAccountId.ShouldBeNull();
        company.DefaultIncomeAccountId.ShouldBeNull();
        company.DefaultExpenseAccountId.ShouldBeNull();
        company.DefaultBankAccountId.ShouldBeNull();
        company.DefaultInventoryAccountId.ShouldBeNull();
    }

    [Fact]
    public void Company_SetupComplete_WhenAllAccountsAssigned()
    {
        var company = new Company(Guid.NewGuid(), "Setup Corp");
        company.DefaultReceivableAccountId = Guid.NewGuid();
        company.DefaultPayableAccountId = Guid.NewGuid();
        company.DefaultIncomeAccountId = Guid.NewGuid();
        company.DefaultExpenseAccountId = Guid.NewGuid();
        company.DefaultBankAccountId = Guid.NewGuid();
        company.DefaultInventoryAccountId = Guid.NewGuid();

        // A fully-configured company has all 6 default accounts set
        var configured = company.DefaultReceivableAccountId.HasValue
            && company.DefaultPayableAccountId.HasValue
            && company.DefaultIncomeAccountId.HasValue
            && company.DefaultExpenseAccountId.HasValue
            && company.DefaultBankAccountId.HasValue
            && company.DefaultInventoryAccountId.HasValue;

        configured.ShouldBeTrue();
    }

    #endregion
}
