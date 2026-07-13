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
}
