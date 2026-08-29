using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// A single note-thread entry attachable to a Lead (and, in future, other CRM parents).
/// Distinct from the flat free-text Notes field already on Lead/Opportunity/Prospect/Contract —
/// this models a chronological, multi-author note thread. Maps to ERPNext CRM Note child table.
/// </summary>
public class CrmNote : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Parent entity type, e.g. "Lead", "Opportunity".</summary>
    public string ParentType { get; set; } = null!;
    public Guid ParentId { get; set; }

    public string NoteText { get; set; } = null!;
    public Guid AddedByUserId { get; set; }
    public DateTime AddedOn { get; set; }

    protected CrmNote() { }

    public CrmNote(Guid id, string parentType, Guid parentId, string noteText, Guid addedByUserId, Guid? tenantId = null)
        : base(id)
    {
        ParentType = Check.NotNullOrWhiteSpace(parentType, nameof(parentType), CrmNoteConsts.MaxParentTypeLength);
        ParentId = Check.NotDefaultOrNull<Guid>(parentId, nameof(parentId));
        NoteText = Check.NotNullOrWhiteSpace(noteText, nameof(noteText), CrmNoteConsts.MaxNoteTextLength);
        AddedByUserId = addedByUserId;
        AddedOn = DateTime.UtcNow;
        TenantId = tenantId;
    }

    public void UpdateNoteText(string noteText)
    {
        NoteText = Check.NotNullOrWhiteSpace(noteText, nameof(noteText), CrmNoteConsts.MaxNoteTextLength);
    }
}
