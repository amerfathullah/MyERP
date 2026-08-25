using System;
using MyERP.Projects.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Projects;

public class TaskTypeTests
{
    [Fact]
    public void TaskType_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var taskType = new TaskType(id, "Bugfix", 2.5m, "Defect resolution");

        Assert.Equal(id, taskType.Id);
        Assert.Equal("Bugfix", taskType.Name);
        Assert.Equal(2.5m, taskType.Weight);
        Assert.Equal("Defect resolution", taskType.Description);
    }

    [Fact]
    public void TaskType_Creation_NegativeWeight_DefaultsToOne()
    {
        var taskType = new TaskType(Guid.NewGuid(), "Feature", -5m);
        Assert.Equal(1m, taskType.Weight);
    }
}
