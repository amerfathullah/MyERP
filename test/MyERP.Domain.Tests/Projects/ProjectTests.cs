using System;
using MyERP.Projects.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Projects;

public class ProjectTests
{
    [Fact]
    public void Create_ShouldSetOpenStatus()
    {
        var project = CreateProject();

        project.Status.ShouldBe(ProjectStatus.Open);
        project.PercentComplete.ShouldBe(0);
    }

    [Fact]
    public void Complete_ShouldSetStatusAndPercent()
    {
        var project = CreateProject();

        project.Complete();

        project.Status.ShouldBe(ProjectStatus.Completed);
        project.PercentComplete.ShouldBe(100);
    }

    [Fact]
    public void Cancel_FromOpen_ShouldSucceed()
    {
        var project = CreateProject();

        project.Cancel();

        project.Status.ShouldBe(ProjectStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromCompleted_ShouldThrow()
    {
        var project = CreateProject();
        project.Complete();

        Assert.Throws<BusinessException>(() => project.Cancel());
    }

    [Fact]
    public void Reopen_FromCancelled_ShouldSucceed()
    {
        var project = CreateProject();
        project.Cancel();

        project.Reopen();

        project.Status.ShouldBe(ProjectStatus.Open);
    }

    [Fact]
    public void UpdateProgress_TaskCompletion_CalculatesCorrectly()
    {
        var project = CreateProject();
        var t1 = new ProjectTask(Guid.NewGuid(), project.Id, "T-001", "Task 1");
        var t2 = new ProjectTask(Guid.NewGuid(), project.Id, "T-002", "Task 2");
        t1.Complete();
        project.Tasks.Add(t1);
        project.Tasks.Add(t2);

        project.UpdateProgress();

        project.PercentComplete.ShouldBe(50); // 1 of 2 completed
    }

    [Fact]
    public void UpdateProgress_TaskProgress_AveragesProgress()
    {
        var project = CreateProject();
        project.PercentCompleteMethod = PercentCompleteMethod.TaskProgress;
        var t1 = new ProjectTask(Guid.NewGuid(), project.Id, "T-001", "Task 1") { Progress = 80 };
        var t2 = new ProjectTask(Guid.NewGuid(), project.Id, "T-002", "Task 2") { Progress = 40 };
        project.Tasks.Add(t1);
        project.Tasks.Add(t2);

        project.UpdateProgress();

        project.PercentComplete.ShouldBe(60); // (80+40)/2
    }

    [Fact]
    public void UpdateProgress_AllComplete_AutoCompletesProject()
    {
        var project = CreateProject();
        var t1 = new ProjectTask(Guid.NewGuid(), project.Id, "T-001", "Task 1");
        t1.Complete();
        project.Tasks.Add(t1);

        project.UpdateProgress();

        project.PercentComplete.ShouldBe(100);
        project.Status.ShouldBe(ProjectStatus.Completed);
    }

    private static Project CreateProject() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "PROJ-0001", "Test Project", Guid.NewGuid());
}

public class ProjectTaskTests
{
    [Fact]
    public void Start_FromOpen_ShouldSucceed()
    {
        var task = CreateTask();

        task.Start();

        task.Status.ShouldBe(ProjectTaskStatus.Working);
        task.ActualStartDate.ShouldNotBeNull();
    }

    [Fact]
    public void Complete_ShouldSetProgressTo100()
    {
        var task = CreateTask();
        task.Start();

        task.Complete();

        task.Status.ShouldBe(ProjectTaskStatus.Completed);
        task.Progress.ShouldBe(100);
        task.ActualEndDate.ShouldNotBeNull();
    }

    [Fact]
    public void Complete_FromCancelled_ShouldThrow()
    {
        var task = CreateTask();
        task.Cancel();

        Assert.Throws<BusinessException>(() => task.Complete());
    }

    [Fact]
    public void Cancel_FromCompleted_ShouldThrow()
    {
        var task = CreateTask();
        task.Start();
        task.Complete();

        Assert.Throws<BusinessException>(() => task.Cancel());
    }

    [Fact]
    public void Reopen_FromCompleted_ShouldResetProgress()
    {
        var task = CreateTask();
        task.Start();
        task.Complete();

        task.Reopen();

        task.Status.ShouldBe(ProjectTaskStatus.Open);
        task.Progress.ShouldBe(0);
    }

    private static ProjectTask CreateTask() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "TASK-001", "Test Task");
}
