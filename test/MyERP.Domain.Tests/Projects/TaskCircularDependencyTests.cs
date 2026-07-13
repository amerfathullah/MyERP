using System;
using MyERP.Projects.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Projects;

public class TaskCircularDependencyTests
{
    [Fact]
    public void TaskDependency_Creation()
    {
        var taskId = Guid.NewGuid();
        var dependsOnId = Guid.NewGuid();
        var dep = new TaskDependency(Guid.NewGuid(), taskId, dependsOnId);

        dep.TaskId.ShouldBe(taskId);
        dep.DependsOnTaskId.ShouldBe(dependsOnId);
    }

    [Fact]
    public void ProjectTask_Dependencies_DefaultsEmpty()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), "T-001", "Build Feature");
        task.Dependencies.ShouldNotBeNull();
        task.Dependencies.Count.ShouldBe(0);
    }

    [Fact]
    public void ProjectTask_CanAddDependency()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), "T-001", "Build Feature");
        var dep = new TaskDependency(Guid.NewGuid(), task.Id, Guid.NewGuid());
        task.Dependencies.Add(dep);
        task.Dependencies.Count.ShouldBe(1);
    }

    [Fact]
    public void ProjectTask_MultipleDependencies()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), "T-003", "Integration");
        task.Dependencies.Add(new TaskDependency(Guid.NewGuid(), task.Id, Guid.NewGuid()));
        task.Dependencies.Add(new TaskDependency(Guid.NewGuid(), task.Id, Guid.NewGuid()));
        task.Dependencies.Add(new TaskDependency(Guid.NewGuid(), task.Id, Guid.NewGuid()));

        task.Dependencies.Count.ShouldBe(3);
    }

    [Fact]
    public void CircularDependencyDetected_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.CircularDependencyDetected.ShouldBe("MyERP:13001");
    }

    [Fact]
    public void DependenciesIncomplete_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.DependenciesIncomplete.ShouldBe("MyERP:13002");
    }

    [Fact]
    public void ProjectTask_SelfDependency_WouldBeCycle()
    {
        var taskId = Guid.NewGuid();
        var dep = new TaskDependency(Guid.NewGuid(), taskId, taskId);
        dep.TaskId.ShouldBe(dep.DependsOnTaskId);
    }

    [Fact]
    public void ProjectTask_Complete_DoesNotCheckDependencies_AtEntityLevel()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), "T-001", "Task");
        task.Start();
        task.Complete();
        task.Status.ShouldBe(ProjectTaskStatus.Completed);
    }

    [Fact]
    public void ProjectTask_AddDependency_SelfReference_Throws()
    {
        var taskId = Guid.NewGuid();
        var task = new ProjectTask(taskId, Guid.NewGuid(), "T-001", "Self");

        var ex = Should.Throw<Volo.Abp.BusinessException>(() => task.AddDependency(taskId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.CircularDependencyDetected);
    }

    [Fact]
    public void ProjectTask_AddDependency_Valid_AddsToDependencies()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), "T-001", "Feature");
        var depId = Guid.NewGuid();

        task.AddDependency(depId);

        task.Dependencies.Count.ShouldBe(1);
        task.Dependencies[0].DependsOnTaskId.ShouldBe(depId);
    }

    [Fact]
    public void ProjectTask_AddDependency_MultipleDifferent_Succeeds()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), "T-001", "Feature");
        task.AddDependency(Guid.NewGuid());
        task.AddDependency(Guid.NewGuid());
        task.Dependencies.Count.ShouldBe(2);
    }
}
