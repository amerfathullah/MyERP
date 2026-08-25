using System;
using MyERP.Projects.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Projects;

public class ProjectUpdateTests
{
    [Fact]
    public void ProjectUpdate_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var date = new DateTime(2026, 8, 25);
        var time = new TimeSpan(14, 30, 0);

        var update = new ProjectUpdate(
            id,
            projectId,
            date,
            percentComplete: 75.5m,
            summary: "Sprint 4 review completed",
            notes: "Delivered user authentication and profile management features.",
            time: time);

        Assert.Equal(id, update.Id);
        Assert.Equal(projectId, update.ProjectId);
        Assert.Equal(date, update.Date);
        Assert.Equal(time, update.Time);
        Assert.Equal(75.5m, update.PercentComplete);
        Assert.Equal("Sprint 4 review completed", update.Summary);
        Assert.Equal("Delivered user authentication and profile management features.", update.Notes);
        Assert.False(update.Sent);
    }
}
