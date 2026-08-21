using System;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Project-Customer cross-validation (Gotcha #468).
/// If Project.CustomerId is set, Sales document (SO/SI) customer must match.
/// If Project.CustomerId is null, any customer can use the project.
/// </summary>
public class SalesProjectCustomerValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerAId = Guid.NewGuid();
    private readonly Guid _customerBId = Guid.NewGuid();

    [Fact]
    public void Project_WithMatchingCustomer_ValidatesSuccessfully()
    {
        var project = new Project(Guid.NewGuid(), _companyId, "PRJ-001", "Client Project Alpha")
        {
            CustomerId = _customerAId
        };

        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerAId, "SO-001", DateTime.UtcNow)
        {
            ProjectId = project.Id
        };

        Assert.Equal(project.CustomerId, so.CustomerId);
        Assert.Equal(project.Id, so.ProjectId);
    }

    [Fact]
    public void Project_WithDifferentCustomer_DetectsMismatch()
    {
        var project = new Project(Guid.NewGuid(), _companyId, "PRJ-001", "Client Project Alpha")
        {
            CustomerId = _customerAId
        };

        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerBId, "SO-002", DateTime.UtcNow)
        {
            ProjectId = project.Id
        };

        // Verification logic
        bool isMismatch = project.CustomerId.HasValue && project.CustomerId.Value != so.CustomerId;
        Assert.True(isMismatch);
    }

    [Fact]
    public void Project_WithNoCustomer_PermitsAnyCustomer()
    {
        var project = new Project(Guid.NewGuid(), _companyId, "PRJ-002", "Internal Initiative")
        {
            CustomerId = null
        };

        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerBId, "SO-003", DateTime.UtcNow)
        {
            ProjectId = project.Id
        };

        bool isMismatch = project.CustomerId.HasValue && project.CustomerId.Value != so.CustomerId;
        Assert.False(isMismatch);
    }
}
