using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Projects.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Projects.DomainServices;

/// <summary>
/// Validates task dependency graph integrity — detects circular dependencies.
/// Per DO-NOT: "Skip circular reference detection on task dependency changes"
/// </summary>
public class TaskDependencyValidationService : DomainService
{
    private readonly IRepository<ProjectTask, Guid> _taskRepository;

    public TaskDependencyValidationService(IRepository<ProjectTask, Guid> taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <summary>
    /// Validates that adding a dependency from taskId → dependsOnTaskId does not create a cycle.
    /// Uses DFS from dependsOnTaskId to see if we can reach taskId.
    /// </summary>
    public async Task ValidateNoCycleAsync(Guid projectId, Guid taskId, Guid dependsOnTaskId)
    {
        if (taskId == dependsOnTaskId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CircularDependencyDetected)
                .WithData("taskId", taskId);
        }

        // Load all tasks for this project with their dependencies
        var queryable = await _taskRepository.GetQueryableAsync();
        var tasks = queryable.Where(t => t.ProjectId == projectId).ToList();

        // Build adjacency list: task → tasks it depends on
        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var task in tasks)
        {
            graph[task.Id] = task.Dependencies.Select(d => d.DependsOnTaskId).ToList();
        }

        // Add the proposed edge
        if (!graph.ContainsKey(taskId))
            graph[taskId] = new List<Guid>();
        graph[taskId].Add(dependsOnTaskId);

        // DFS from dependsOnTaskId to check if we can reach taskId (which would mean cycle)
        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(dependsOnTaskId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == taskId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CircularDependencyDetected)
                    .WithData("taskId", taskId)
                    .WithData("dependsOnTaskId", dependsOnTaskId);
            }

            if (!visited.Add(current)) continue;

            if (graph.TryGetValue(current, out var deps))
            {
                foreach (var dep in deps)
                {
                    stack.Push(dep);
                }
            }
        }
    }

    /// <summary>
    /// Validates that a task's dependencies are all completed before allowing completion.
    /// Per DO-NOT: "Allow tasks to be marked Completed when dependencies are incomplete"
    /// </summary>
    public async Task ValidateDependenciesCompletedAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        if (task.Dependencies.Count == 0) return;

        var depTaskIds = task.Dependencies.Select(d => d.DependsOnTaskId).ToList();
        var queryable = await _taskRepository.GetQueryableAsync();
        var incompleteDeps = queryable
            .Where(t => depTaskIds.Contains(t.Id) && t.Status != ProjectTaskStatus.Completed)
            .ToList();

        if (incompleteDeps.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DependenciesIncomplete)
                .WithData("taskId", taskId)
                .WithData("incompleteDependencies", string.Join(", ", incompleteDeps.Select(t => t.Subject)));
        }
    }

    /// <summary>
    /// Refuses to convert a group task with children into a non-group task.
    /// Per ERPNext PR #69181 (<c>task.py: validate_group_has_no_children</c>).
    /// </summary>
    public async Task ValidateGroupHasNoChildrenAsync(ProjectTask task, bool newIsGroup)
    {
        if (newIsGroup || !task.IsGroup) return;

        var queryable = await _taskRepository.GetQueryableAsync();
        var childTasks = queryable
            .Where(t => t.ParentTaskId == task.Id)
            .Select(t => t.TaskNumber)
            .ToList();

        if (childTasks.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot convert Task to non-group because the following child Tasks exist: {string.Join(", ", childTasks)}.");
        }
    }

    /// <summary>
    /// Refuses setting a group task's ExpectedEndDate earlier than any non-cancelled child task's ExpectedEndDate.
    /// Per ERPNext PR #69181 (<c>task.py: validate_children_expected_end_date</c>).
    /// </summary>
    public async Task ValidateChildrenExpectedEndDateAsync(ProjectTask task, DateTime? newExpectedEndDate)
    {
        if (!newExpectedEndDate.HasValue) return;

        var queryable = await _taskRepository.GetQueryableAsync();
        var endDate = newExpectedEndDate.Value.Date;
        var laterChildren = queryable
            .Where(t => t.ParentTaskId == task.Id
                && t.Status != ProjectTaskStatus.Cancelled
                && t.ExpectedEndDate.HasValue
                && t.ExpectedEndDate.Value.Date > endDate)
            .Select(t => t.TaskNumber)
            .ToList();

        if (laterChildren.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Expected End Date cannot be before the Expected End Date of child tasks {string.Join(", ", laterChildren)}.");
        }
    }

    /// <summary>
    /// Refuses setting a child task's ExpectedEndDate later than its parent group's ExpectedEndDate.
    /// Per ERPNext PR #69181 (<c>task.py: validate_parent_expected_end_date</c>).
    /// </summary>
    public static void ValidateParentExpectedEndDate(ProjectTask task, ProjectTask? parentTask)
    {
        if (parentTask == null || !task.ExpectedEndDate.HasValue || !parentTask.ExpectedEndDate.HasValue)
            return;

        if (task.ExpectedEndDate.Value.Date > parentTask.ExpectedEndDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Expected End Date cannot be greater than the parent task '{parentTask.Subject}' Expected End Date.");
        }
    }

    /// <summary>
    /// Extends a group task (and its ancestor chain) when a holiday pushes a child task past the group's end date.
    /// Per ERPNext PR #69182 (<c>project.py: extend_group_to_cover</c>).
    /// </summary>
    public static void ExtendGroupToCover(ProjectTask group, DateTime? endDate, IReadOnlyList<ProjectTask> projectTasks)
    {
        if (!endDate.HasValue || !group.ExpectedEndDate.HasValue || endDate.Value.Date <= group.ExpectedEndDate.Value.Date)
            return;

        if (group.ParentTaskId.HasValue)
        {
            var parentGroup = projectTasks.FirstOrDefault(pt => pt.Id == group.ParentTaskId.Value);
            if (parentGroup != null)
            {
                ExtendGroupToCover(parentGroup, endDate, projectTasks);
            }
        }

        group.ExpectedEndDate = endDate;
    }

    public static DateTime? GetLatestDate(params DateTime?[] dates)
    {
        DateTime? latest = null;
        foreach (var d in dates)
        {
            if (d.HasValue && (!latest.HasValue || d.Value > latest.Value))
                latest = d.Value;
        }
        return latest;
    }

    /// <summary>
    /// Cascades date shifts to all dependent tasks when a task's schedule end date changes.
    /// Per ERPNext PRs #69135 &amp; #69136 (<c>task.py: reschedule_dependent_tasks</c>):
    /// - Uses the later of ExpectedEndDate and ActualEndDate.
    /// - Moves Open, Working, and Overdue dependents (including those without ExpectedEndDate) when ExpectedStartDate &lt;= endDate.
    /// - Preserves time-of-day and duration.
    /// - When predecessor's end moves earlier, pulls back dependents scheduled on the day after the old end,
    ///   respecting any other predecessors' latest end date.
    /// </summary>
    public async Task RescheduleDependentTasksAsync(
        ProjectTask task,
        HashSet<Guid>? visited = null,
        DateTime? previousEndDate = null)
    {
        var endDate = task.GetScheduleEndDate();
        if (!endDate.HasValue) return;

        visited ??= new HashSet<Guid>();
        if (!visited.Add(task.Id)) return; // Prevent infinite recursion

        var queryable = await _taskRepository.GetQueryableAsync();
        var projectTasks = queryable.Where(t => t.ProjectId == task.ProjectId).ToList();
        var tasksById = projectTasks.ToDictionary(t => t.Id);

        // Find tasks that depend on this task
        var dependentTasks = projectTasks
            .Where(t => t.Dependencies.Any(d => d.DependsOnTaskId == task.Id))
            .ToList();

        if (dependentTasks.Count == 0) return;

        var movedEarlier = previousEndDate.HasValue && previousEndDate.Value.Date > endDate.Value.Date;
        DateTime? dayAfterPreviousEnd = movedEarlier ? previousEndDate!.Value.Date.AddDays(1) : null;

        foreach (var depTask in dependentTasks)
        {
            if (!depTask.ExpectedStartDate.HasValue
                || depTask.Status is not (ProjectTaskStatus.Open or ProjectTaskStatus.Working or ProjectTaskStatus.Overdue))
            {
                continue;
            }

            var depPreviousEnd = depTask.GetScheduleEndDate();

            if (depTask.ExpectedStartDate.Value <= endDate.Value)
            {
                depTask.MoveToStartAfter(endDate.Value);
            }
            else if (movedEarlier && depTask.ExpectedStartDate.Value.Date == dayAfterPreviousEnd!.Value)
            {
                // Scheduled from the old end: move it back, but not before another predecessor ends.
                DateTime? otherPredEnd = null;
                foreach (var dep in depTask.Dependencies)
                {
                    if (dep.DependsOnTaskId == task.Id) continue;
                    if (tasksById.TryGetValue(dep.DependsOnTaskId, out var otherPred))
                    {
                        otherPredEnd = GetLatestDate(otherPredEnd, otherPred.GetScheduleEndDate());
                    }
                }

                var newEndDate = GetLatestDate(endDate, otherPredEnd)!.Value;
                if (newEndDate.Date >= previousEndDate!.Value.Date)
                {
                    continue;
                }

                depTask.MoveToStartAfter(newEndDate);
            }
            else
            {
                continue;
            }

            await _taskRepository.UpdateAsync(depTask);

            // Cascade to downstream dependent tasks
            await RescheduleDependentTasksAsync(depTask, visited, depPreviousEnd);
        }
    }
}
