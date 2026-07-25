using System;
using MyERP.HumanResources.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for employee company filtering, leave form improvements, and SaveShortcut coverage.
/// </summary>
public class CompanyFilteringAndFormTests
{
    // === Employee Company Scoping ===

    [Fact]
    public void Employee_HasCompanyId()
    {
        var companyId = Guid.NewGuid();
        var emp = new Employee(Guid.NewGuid(), companyId, "EMP-001", "John", Guid.NewGuid());
        emp.CompanyId.ShouldBe(companyId);
    }

    [Fact]
    public void Employee_DifferentCompanies_HaveDifferentIds()
    {
        var company1 = Guid.NewGuid();
        var company2 = Guid.NewGuid();
        var emp1 = new Employee(Guid.NewGuid(), company1, "EMP-001", "Alice", Guid.NewGuid());
        var emp2 = new Employee(Guid.NewGuid(), company2, "EMP-002", "Bob", Guid.NewGuid());

        emp1.CompanyId.ShouldNotBe(emp2.CompanyId);
    }

    [Fact]
    public void Employee_FullName_WithLastName()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-003", "Jane", Guid.NewGuid());
        emp.LastName = "Smith";
        emp.FullName.ShouldBe("Jane Smith");
    }

    [Fact]
    public void Employee_FullName_WithoutLastName()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-004", "Solo", Guid.NewGuid());
        emp.FullName.ShouldBe("Solo");
    }

    // === Leave Application Entity ===

    [Fact]
    public void LeaveApplication_RequiresEmployeeId()
    {
        // LeaveApplication entity requires employeeId in constructor
        var leave = new MyERP.HumanResources.Entities.LeaveApplication(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(2), 3, Guid.NewGuid());
        leave.EmployeeId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void LeaveApplication_RequiresCompanyId()
    {
        var companyId = Guid.NewGuid();
        var leave = new MyERP.HumanResources.Entities.LeaveApplication(
            Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddDays(1), 1, Guid.NewGuid());
        leave.CompanyId.ShouldBe(companyId);
    }

    // === Quotation Update Workflow ===

    [Fact]
    public void Quotation_UpdateFieldsInDraft()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Widget", 5, 100, 0);

        // Update fields (simulates edit-mode save)
        q.Notes = "Updated by customer request";
        q.CurrencyCode = "USD";

        q.Notes.ShouldBe("Updated by customer request");
        q.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void Quotation_ReplaceItems_ClearAndReAdd()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Original A", 2, 50, 0);
        q.AddItem(Guid.NewGuid(), "Original B", 3, 75, 0);
        q.Items.Count.ShouldBe(2);
        q.NetTotal.ShouldBe(325m); // (2*50) + (3*75)

        // Replace items (simulates UpdateAsync pattern)
        q.ClearItems();
        q.AddItem(Guid.NewGuid(), "Replacement X", 10, 20, 0);

        q.Items.Count.ShouldBe(1);
        q.NetTotal.ShouldBe(200m); // 10*20
    }

    [Fact]
    public void Quotation_CannotUpdateAfterSubmit()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Item", 1, 100, 0);
        q.Submit();

        // Attempt to add item after submit — should throw
        Should.Throw<BusinessException>(() => q.AddItem(Guid.NewGuid(), "New", 1, 50, 0));
    }

    private static Quotation CreateQuotation() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-TEST", DateTime.Today, Guid.NewGuid());
}
