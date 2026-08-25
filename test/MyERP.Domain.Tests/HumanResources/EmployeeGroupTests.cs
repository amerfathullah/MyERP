using System;
using System.Linq;
using MyERP.HumanResources.Entities;
using Xunit;

namespace MyERP.Domain.Tests.HumanResources;

public class EmployeeGroupTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void EmployeeGroup_Creation_And_MemberManagement()
    {
        var group = new EmployeeGroup(Guid.NewGuid(), _companyId, "Shift A Operations");
        var emp1 = Guid.NewGuid();
        var emp2 = Guid.NewGuid();

        group.AddEmployee(emp1, "Ahmad Faiz", "Machine Operator");
        group.AddEmployee(emp2, "Siti Nurhaliza", "Quality Inspector");

        Assert.Equal("Shift A Operations", group.GroupName);
        Assert.Equal(2, group.Items.Count);
        Assert.Contains(group.Items, i => i.EmployeeId == emp1 && i.EmployeeName == "Ahmad Faiz");

        // Duplicate add is idempotent
        group.AddEmployee(emp1, "Ahmad Faiz", "Machine Operator");
        Assert.Equal(2, group.Items.Count);

        // Remove employee
        group.RemoveEmployee(emp1);
        Assert.Single(group.Items);
        Assert.Equal(emp2, group.Items.First().EmployeeId);

        // Clear
        group.ClearEmployees();
        Assert.Empty(group.Items);
    }

    [Fact]
    public void EmployeeGroup_Constructor_ThrowsOnEmptyName()
    {
        Assert.Throws<ArgumentException>(() =>
            new EmployeeGroup(Guid.NewGuid(), _companyId, "")
        );
    }
}
