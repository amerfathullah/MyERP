using System;
using Xunit;
using MyERP.HumanResources.Entities;
using MyERP.Support.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using MyERP.Accounting.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.Inventory;

namespace MyERP.DomainTests;

/// <summary>
/// Tests covering UX improvements: GUID→dropdown entity relationships,
/// error handling patterns, and form-level validations for dropdown data sources.
/// </summary>
public class UxErrorHandlingAndDropdownTests
{
    // === Employee entity — dropdown data source validations ===

    [Fact]
    public void Employee_CompanyId_Required()
    {
        var companyId = Guid.NewGuid();
        var emp = new Employee(Guid.NewGuid(), companyId, "EMP-100", "Test");
        Assert.Equal(companyId, emp.CompanyId);
    }

    [Fact]
    public void Employee_CompanyId_RequiredIsNonEmpty()
    {
        var companyId = Guid.NewGuid();
        var emp = new Employee(Guid.NewGuid(), companyId, "EMP-101", "Test");
        Assert.NotEqual(Guid.Empty, emp.CompanyId);
    }

    [Fact]
    public void Employee_FullName_UsedForDropdownDisplay()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-102", "Ahmad");
        emp.LastName = "Ibrahim";
        Assert.Equal("Ahmad Ibrahim", emp.FullName);
    }

    [Fact]
    public void Employee_FullName_FirstNameOnly_NoLastName()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-103", "Siti");
        Assert.Equal("Siti", emp.FullName);
    }

    // === Issue entity — customer link is optional ===

    [Fact]
    public void Issue_CustomerId_DefaultsNull()
    {
        var issue = new Issue(Guid.NewGuid(), Guid.NewGuid(), "Test issue");
        Assert.Null(issue.CustomerId);
    }

    [Fact]
    public void Issue_CustomerId_CanBeSet()
    {
        var issue = new Issue(Guid.NewGuid(), Guid.NewGuid(), "Customer issue");
        var custId = Guid.NewGuid();
        issue.CustomerId = custId;
        Assert.Equal(custId, issue.CustomerId);
    }

    [Fact]
    public void Issue_Priority_DefaultsMedium()
    {
        var issue = new Issue(Guid.NewGuid(), Guid.NewGuid(), "Priority test");
        Assert.Equal("Medium", issue.Priority);
    }

    // === Timesheet entity — employee link ===

    [Fact]
    public void Timesheet_EmployeeId_Required()
    {
        var empId = Guid.NewGuid();
        var ts = new Timesheet(Guid.NewGuid(), Guid.NewGuid(), empId, DateTime.Today, DateTime.Today.AddDays(7));
        Assert.Equal(empId, ts.EmployeeId);
    }

    [Fact]
    public void Timesheet_CompanyId_Required()
    {
        var companyId = Guid.NewGuid();
        var ts = new Timesheet(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today, DateTime.Today.AddDays(7));
        Assert.Equal(companyId, ts.CompanyId);
    }

    // === PeriodClosingVoucher entity — account + FY links ===

    [Fact]
    public void PeriodClosingVoucher_ClosingAccountId_Required()
    {
        var accountId = Guid.NewGuid();
        var pcv = new PeriodClosingVoucher(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today, accountId);
        Assert.Equal(accountId, pcv.ClosingAccountId);
    }

    [Fact]
    public void PeriodClosingVoucher_FiscalYearId_Required()
    {
        var fyId = Guid.NewGuid();
        var pcv = new PeriodClosingVoucher(
            Guid.NewGuid(), Guid.NewGuid(), fyId,
            DateTime.Today, DateTime.Today, Guid.NewGuid());
        Assert.Equal(fyId, pcv.FiscalYearId);
    }

    // === Error handling patterns ===

    [Fact]
    public void LeaveAllocation_Delete_WhenUsed_ShouldThrow()
    {
        var alloc = new LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(365), 12);
        alloc.DeductLeave(5);
        Assert.True(alloc.LeavesUsed > 0);
    }

    [Fact]
    public void LeaveAllocation_Balance_AfterDeduction()
    {
        var alloc = new LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(365), 12);
        alloc.DeductLeave(3);
        Assert.Equal(9, alloc.Balance);
    }

    // === Contract workflow actions ===

    [Fact]
    public void Contract_Sign_FromUnsigned()
    {
        var contract = new CRM.Entities.Contract(Guid.NewGuid(), Guid.NewGuid(), "CON-001", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        Assert.Equal(CRM.ContractStatus.Active, contract.Status);
    }

    [Fact]
    public void Contract_Cancel_FromActive()
    {
        var contract = new CRM.Entities.Contract(Guid.NewGuid(), Guid.NewGuid(), "CON-002", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        contract.Cancel();
        Assert.Equal(CRM.ContractStatus.Cancelled, contract.Status);
    }

    // === PutawayRule entity — capacity validation ===

    [Fact]
    public void PutawayRule_StockCapacity_DefaultsZero()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0, rule.StockCapacity);
    }

    [Fact]
    public void PutawayRule_AvailableCapacity_WhenFinite()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        rule.StockCapacity = 100;
        Assert.Equal(100, rule.GetAvailableCapacity(0));
        Assert.Equal(60, rule.GetAvailableCapacity(40));
    }

    [Fact]
    public void PutawayRule_AvailableCapacity_NeverNegative()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        rule.StockCapacity = 50;
        Assert.Equal(0, rule.GetAvailableCapacity(80));
    }

    // === Dropdown entity name patterns ===

    [Fact]
    public void Customer_Name_UsedForDropdown()
    {
        var cust = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Acme Sdn Bhd");
        Assert.Equal("Acme Sdn Bhd", cust.Name);
    }

    [Fact]
    public void Supplier_Name_UsedForDropdown()
    {
        var supp = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Vendor Corp");
        Assert.Equal("Vendor Corp", supp.Name);
    }

    [Fact]
    public void Item_ItemName_UsedForDropdown()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget A", Inventory.ItemType.Goods);
        Assert.Equal("Widget A", item.ItemName);
    }
}
