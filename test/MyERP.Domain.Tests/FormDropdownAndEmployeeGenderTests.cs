using System;
using Xunit;
using MyERP.HumanResources.Entities;
using MyERP.HumanResources;

namespace MyERP.DomainTests;

/// <summary>
/// Tests for Employee Gender field, form dropdown entity relationships,
/// and employee entity enhancements for ERPNext parity.
/// </summary>
public class FormDropdownAndEmployeeGenderTests
{
    // === Employee Gender Field ===

    [Fact]
    public void Employee_Gender_DefaultsNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-001", "John");
        Assert.Null(emp.Gender);
    }

    [Fact]
    public void Employee_Gender_CanBeSet_Male()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-002", "Ali");
        emp.Gender = "Male";
        Assert.Equal("Male", emp.Gender);
    }

    [Fact]
    public void Employee_Gender_CanBeSet_Female()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-003", "Siti");
        emp.Gender = "Female";
        Assert.Equal("Female", emp.Gender);
    }

    [Fact]
    public void Employee_Gender_CanBeSet_Other()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-004", "Alex");
        emp.Gender = "Other";
        Assert.Equal("Other", emp.Gender);
    }

    // === Employee Entity — FullName Combinations ===

    [Fact]
    public void Employee_FullName_FirstAndLast()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-005", "Ahmad");
        emp.LastName = "bin Ibrahim";
        Assert.Equal("Ahmad bin Ibrahim", emp.FullName);
    }

    [Fact]
    public void Employee_FullName_FirstOnly_WhenLastNameEmpty()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-006", "Priya");
        emp.LastName = "";
        Assert.Equal("Priya", emp.FullName);
    }

    [Fact]
    public void Employee_FullName_FirstOnly_WhenLastNameNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-007", "Ravi");
        Assert.Equal("Ravi", emp.FullName);
    }

    // === Employee Entity — Department & Designation (dropdown data) ===

    [Fact]
    public void Employee_Designation_CanBeSet()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-008", "Wei");
        emp.Designation = "Senior Engineer";
        Assert.Equal("Senior Engineer", emp.Designation);
    }

    [Fact]
    public void Employee_Department_CanBeSet()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-009", "Lim");
        emp.Department = "Research & Development";
        Assert.Equal("Research & Development", emp.Department);
    }

    [Fact]
    public void Employee_Designation_DefaultsNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-010", "Mei");
        Assert.Null(emp.Designation);
    }

    [Fact]
    public void Employee_Department_DefaultsNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-011", "Tan");
        Assert.Null(emp.Department);
    }

    // === Employee Entity — Employment Status ===

    [Fact]
    public void Employee_Status_DefaultsActive()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-012", "Raj");
        Assert.Equal(EmploymentStatus.Active, emp.Status);
    }

    [Fact]
    public void Employee_Status_CanBeResigned()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-013", "Kumar");
        emp.Status = EmploymentStatus.Resigned;
        Assert.Equal(EmploymentStatus.Resigned, emp.Status);
    }

    [Fact]
    public void Employee_DateOfResignation_DefaultsNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-014", "Ling");
        Assert.Null(emp.DateOfResignation);
    }

    [Fact]
    public void Employee_DateOfResignation_CanBeSet()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-015", "Chen");
        var resignDate = new DateTime(2026, 12, 31);
        emp.DateOfResignation = resignDate;
        Assert.Equal(resignDate, emp.DateOfResignation);
    }

    // === Employee Entity — Statutory Numbers ===

    [Fact]
    public void Employee_EpfNumber_DefaultsNull()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-016", "Aziz");
        Assert.Null(emp.EpfNumber);
    }

    [Fact]
    public void Employee_EpfNumber_CanBeSet()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-017", "Fatimah");
        emp.EpfNumber = "EPF-12345678";
        Assert.Equal("EPF-12345678", emp.EpfNumber);
    }

    [Fact]
    public void Employee_Citizenship_DefaultsMalaysian()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-018", "Noor");
        Assert.Equal(CitizenshipType.Malaysian, emp.Citizenship);
    }

    // === Employee DTO — Gender field ===

    [Fact]
    public void EmployeeDto_Gender_Exists()
    {
        var dto = new EmployeeDto();
        Assert.Null(dto.Gender);
        dto.Gender = "Male";
        Assert.Equal("Male", dto.Gender);
    }

    [Fact]
    public void CreateUpdateEmployeeDto_Gender_Exists()
    {
        var dto = new CreateUpdateEmployeeDto
        {
            CompanyId = Guid.NewGuid(),
            FirstName = "Test",
            Gender = "Female"
        };
        Assert.Equal("Female", dto.Gender);
    }
}
