using System;
using Volo.Abp.Domain.Entities;

namespace MyERP.Projects.Entities;

public class TaskDependency : Entity<Guid>
{
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }

    protected TaskDependency() { }

    public TaskDependency(Guid id, Guid taskId, Guid dependsOnTaskId)
        : base(id)
    {
        TaskId = taskId;
        DependsOnTaskId = dependsOnTaskId;
    }
}
