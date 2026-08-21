using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Projects.DomainServices;
using MyERP.Projects.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Projects;

/// <summary>
/// Unit tests for Task Dependency date rescheduling.
/// Verifies rules migrated from erpnext/projects/doctype/task/task.py (Gotcha #1302).
/// </summary>
public class TaskDependencyRescheduleTests
{
    private readonly IRepository<ProjectTask, Guid> _taskRepository = Substitute.For<IRepository<ProjectTask, Guid>>();
    private readonly TaskDependencyValidationService _service;
    private readonly Guid _projectId = Guid.NewGuid();

    public TaskDependencyRescheduleTests()
    {
        _service = new TaskDependencyValidationService(_taskRepository);
    }

    [Fact]
    public async Task RescheduleDependentTasksAsync_CascadesDateShiftToDependentTasks()
    {
        // Task A: 2026-08-01 to 2026-08-10 (10 days)
        var taskA = new ProjectTask(Guid.NewGuid(), _projectId, "TASK-A", "Design Foundation")
        {
            ExpectedStartDate = new DateTime(2026, 8, 1),
            ExpectedEndDate = new DateTime(2026, 8, 10)
        };

        // Task B depends on Task A: originally 2026-08-05 to 2026-08-09 (4 days duration)
        var taskB = new ProjectTask(Guid.NewGuid(), _projectId, "TASK-B", "Build Framework")
        {
            ExpectedStartDate = new DateTime(2026, 8, 5),
            ExpectedEndDate = new DateTime(2026, 8, 9)
        };
        taskB.AddDependency(taskA.Id);

        var taskList = new List<ProjectTask> { taskA, taskB };
        _taskRepository.GetQueryableAsync().Returns(Task.FromResult(taskList.AsQueryable()));

        // Act: Task A delayed to end on 2026-08-15
        taskA.ExpectedEndDate = new DateTime(2026, 8, 15);
        await _service.RescheduleDependentTasksAsync(taskA);

        // Assert: Task B must now start on 2026-08-16 (day after Task A end) and end on 2026-08-20 (maintaining 4-day duration)
        Assert.Equal(new DateTime(2026, 8, 16), taskB.ExpectedStartDate);
        Assert.Equal(new DateTime(2026, 8, 20), taskB.ExpectedEndDate);
    }
}
