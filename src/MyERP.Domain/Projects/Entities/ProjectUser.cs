using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Projects.Entities;

/// <summary>
/// A user assigned to a project's team.
/// Maps to ERPNext projects/doctype/project_user (Project.users child table).
/// </summary>
public class ProjectUser : CreationAuditedEntity<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Whether this user can view attachments on the project.</summary>
    public bool ViewAttachments { get; set; }

    /// <summary>Whether timesheets are hidden from this user on the project.</summary>
    public bool HideTimesheets { get; set; }

    /// <summary>Whether the welcome email has been sent to this user.</summary>
    public bool WelcomeEmailSent { get; set; }

    protected ProjectUser() { }

    public ProjectUser(Guid id, Guid projectId, Guid userId) : base(id)
    {
        ProjectId = projectId;
        UserId = userId;
    }
}
