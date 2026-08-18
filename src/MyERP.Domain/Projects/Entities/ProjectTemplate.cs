using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Project Template — a reusable, self-contained set of task definitions (with intra-template
/// dependency edges) that can be instantiated onto a new Project, cloning each template task
/// into a real ProjectTask and remapping dependencies to the new tasks. Maps to ERPNext
/// projects/doctype/project_template.
///
/// Unlike ERPNext (where template tasks are links to standalone, project-independent Task
/// records), MyERP's ProjectTask always belongs to a Project, so the template owns its own
/// task definitions instead of referencing external ones — dependency validation simplifies
/// to "the target must be another task within this same template".
/// </summary>
public class ProjectTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string TemplateName { get; set; } = null!;
    public bool Disabled { get; set; }

    private readonly List<ProjectTemplateTask> _tasks = new();
    public IReadOnlyList<ProjectTemplateTask> Tasks => _tasks.AsReadOnly();

    protected ProjectTemplate() { }

    public ProjectTemplate(Guid id, string templateName, Guid? tenantId = null) : base(id)
    {
        TemplateName = Check.NotNullOrWhiteSpace(templateName, nameof(templateName), 140);
        TenantId = tenantId;
    }

    /// <summary>
    /// Replaces the task list. <paramref name="tasks"/> carry a caller-assigned Guid key
    /// (unique within this call) so dependency rows can reference sibling tasks before
    /// database ids exist.
    /// </summary>
    public void SetTasks(IEnumerable<(Guid Key, string Subject, decimal TaskWeight, decimal ExpectedHours, bool IsMilestone, List<Guid> DependsOnKeys)> tasks)
    {
        var taskList = tasks.ToList();
        var keys = taskList.Select(t => t.Key).ToHashSet();

        foreach (var t in taskList)
        {
            foreach (var dep in t.DependsOnKeys)
            {
                if (!keys.Contains(dep))
                    throw new BusinessException(MyERPDomainErrorCodes.ProjectTemplateDependencyNotInTemplate)
                        .WithData("task", t.Subject);
                if (dep == t.Key)
                    throw new BusinessException(MyERPDomainErrorCodes.CircularDependencyDetected)
                        .WithData("task", t.Subject);
            }
        }

        _tasks.Clear();
        var idByKey = taskList.ToDictionary(t => t.Key, _ => Guid.NewGuid());
        foreach (var t in taskList)
        {
            var task = new ProjectTemplateTask(idByKey[t.Key], Id, t.Subject, t.TaskWeight, t.ExpectedHours, t.IsMilestone);
            foreach (var dep in t.DependsOnKeys)
                task.AddDependency(idByKey[dep]);
            _tasks.Add(task);
        }
    }
}

public class ProjectTemplateTask : FullAuditedEntity<Guid>
{
    public Guid ProjectTemplateId { get; set; }
    public string Subject { get; set; } = null!;
    public decimal TaskWeight { get; set; } = 1;
    public decimal ExpectedHours { get; set; }
    public bool IsMilestone { get; set; }

    private readonly List<ProjectTemplateTaskDependency> _dependencies = new();
    public IReadOnlyList<ProjectTemplateTaskDependency> Dependencies => _dependencies.AsReadOnly();

    protected ProjectTemplateTask() { }

    public ProjectTemplateTask(Guid id, Guid projectTemplateId, string subject, decimal taskWeight, decimal expectedHours, bool isMilestone)
        : base(id)
    {
        ProjectTemplateId = projectTemplateId;
        Subject = subject;
        TaskWeight = taskWeight;
        ExpectedHours = expectedHours;
        IsMilestone = isMilestone;
    }

    public void AddDependency(Guid dependsOnTaskId)
        => _dependencies.Add(new ProjectTemplateTaskDependency(Guid.NewGuid(), Id, dependsOnTaskId));
}

public class ProjectTemplateTaskDependency : Entity<Guid>
{
    public Guid ProjectTemplateTaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }

    protected ProjectTemplateTaskDependency() { }

    public ProjectTemplateTaskDependency(Guid id, Guid projectTemplateTaskId, Guid dependsOnTaskId) : base(id)
    {
        ProjectTemplateTaskId = projectTemplateTaskId;
        DependsOnTaskId = dependsOnTaskId;
    }
}
