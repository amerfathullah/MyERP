using System;
using MyERP.Projects;
using MyERP.Projects.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for upstream fix: Project % complete validation when using Manual method.
/// ERPNext PR #57274: percent_complete must be between 0 and 100 when method is Manual.
/// </summary>
public class ProjectPercentCompleteTests
{
    private static Project CreateManualProject()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-001", "Test Project");
        project.PercentCompleteMethod = PercentCompleteMethod.Manual;
        return project;
    }

    [Fact]
    public void SetPercentComplete_ValidValue_Succeeds()
    {
        var project = CreateManualProject();
        project.SetPercentComplete(50);
        Assert.Equal(50m, project.PercentComplete);
    }

    [Fact]
    public void SetPercentComplete_Zero_Succeeds()
    {
        var project = CreateManualProject();
        project.SetPercentComplete(0);
        Assert.Equal(0m, project.PercentComplete);
    }

    [Fact]
    public void SetPercentComplete_Hundred_Succeeds()
    {
        var project = CreateManualProject();
        project.SetPercentComplete(100);
        Assert.Equal(100m, project.PercentComplete);
    }

    [Fact]
    public void SetPercentComplete_NegativeValue_Throws()
    {
        var project = CreateManualProject();
        var ex = Assert.Throws<BusinessException>(() => project.SetPercentComplete(-1));
        Assert.Equal("MyERP:13003", ex.Code);
    }

    [Fact]
    public void SetPercentComplete_Over100_Throws()
    {
        var project = CreateManualProject();
        var ex = Assert.Throws<BusinessException>(() => project.SetPercentComplete(101));
        Assert.Equal("MyERP:13003", ex.Code);
    }

    [Fact]
    public void SetPercentComplete_NonManualMethod_Throws()
    {
        var project = new Project(Guid.NewGuid(), Guid.NewGuid(), "PRJ-002", "Auto Project");
        // Default method is TaskCompletion
        Assert.Throws<BusinessException>(() => project.SetPercentComplete(50));
    }

    [Fact]
    public void SetPercentComplete_CompletedProject_AlwaysSets100()
    {
        var project = CreateManualProject();
        project.Complete(); // Status = Completed, PercentComplete = 100
        
        // Even if we try to set 50, Completed projects should stay at 100
        project.SetPercentComplete(50);
        Assert.Equal(100m, project.PercentComplete);
    }

    [Fact]
    public void SetPercentComplete_ProgressiveUpdate()
    {
        var project = CreateManualProject();
        project.SetPercentComplete(25);
        Assert.Equal(25m, project.PercentComplete);
        
        project.SetPercentComplete(75);
        Assert.Equal(75m, project.PercentComplete);
        
        project.SetPercentComplete(100);
        Assert.Equal(100m, project.PercentComplete);
    }

    [Fact]
    public void SetPercentComplete_BoundaryValues()
    {
        var project = CreateManualProject();
        
        // Just inside valid range
        project.SetPercentComplete(0.01m);
        Assert.Equal(0.01m, project.PercentComplete);
        
        project.SetPercentComplete(99.99m);
        Assert.Equal(99.99m, project.PercentComplete);
    }
}
