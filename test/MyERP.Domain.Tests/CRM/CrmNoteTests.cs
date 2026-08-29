using System;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.CRM;

public class CrmNoteTests
{
    [Fact]
    public void Create_LeadNote_ShouldSetProperties()
    {
        var lead = new Lead(Guid.NewGuid(), Guid.NewGuid(), "LEAD-001", "Alice");
        var userId = Guid.NewGuid();
        var noteId = Guid.NewGuid();

        var note = lead.AddNote(noteId, "Called prospect, discussed requirements", userId);

        note.Id.ShouldBe(noteId);
        note.ParentType.ShouldBe("Lead");
        note.ParentId.ShouldBe(lead.Id);
        note.NoteText.ShouldBe("Called prospect, discussed requirements");
        note.AddedByUserId.ShouldBe(userId);
        note.AddedOn.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Fact]
    public void Create_OpportunityNote_ShouldSetProperties()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-001", "Software Deal");
        var userId = Guid.NewGuid();
        var noteId = Guid.NewGuid();

        var note = opp.AddNote(noteId, "Sent revised pricing proposal", userId);

        note.Id.ShouldBe(noteId);
        note.ParentType.ShouldBe("Opportunity");
        note.ParentId.ShouldBe(opp.Id);
        note.NoteText.ShouldBe("Sent revised pricing proposal");
        note.AddedByUserId.ShouldBe(userId);
    }

    [Fact]
    public void UpdateNoteText_ValidText_ShouldUpdate()
    {
        var note = new CrmNote(Guid.NewGuid(), "Lead", Guid.NewGuid(), "Initial note", Guid.NewGuid());

        note.UpdateNoteText("Updated note text with more details");

        note.NoteText.ShouldBe("Updated note text with more details");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateNoteText_EmptyText_ShouldThrow(string? invalidText)
    {
        var note = new CrmNote(Guid.NewGuid(), "Lead", Guid.NewGuid(), "Initial note", Guid.NewGuid());

        Should.Throw<ArgumentException>(() => note.UpdateNoteText(invalidText!));
    }
}
