using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.CRM;

public class CrmNoteDto : EntityDto<Guid>
{
    public string ParentType { get; set; } = null!;
    public Guid ParentId { get; set; }
    public string NoteText { get; set; } = null!;
    public Guid AddedByUserId { get; set; }
    public DateTime AddedOn { get; set; }
}

public class AddCrmNoteDto
{
    [Required][StringLength(CrmNoteConsts.MaxNoteTextLength)] public string NoteText { get; set; } = null!;
}

public class UpdateCrmNoteDto
{
    [Required][StringLength(CrmNoteConsts.MaxNoteTextLength)] public string NoteText { get; set; } = null!;
}

