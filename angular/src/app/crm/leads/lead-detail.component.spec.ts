import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { LeadDetailComponent } from './lead-detail.component';
import { LeadStore } from '../store/lead.store';
import { LeadService } from '../../proxy/crm/lead.service';

describe('LeadDetailComponent Notes', () => {
  function createComponent(confirmStatus = Confirmation.Status.confirm) {
    const notesList = [
      { id: 'note-1', noteText: 'Initial call completed', addedOn: '2026-11-15T10:00:00Z' },
    ];
    const leadServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'lead-1', leadNumber: 'LD-001', status: 0 })),
      getNotes: vi.fn().mockReturnValue(of(notesList)),
      addNote: vi.fn().mockReturnValue(of({})),
      editNote: vi.fn().mockReturnValue(of({ id: 'note-1', noteText: 'Follow-up scheduled' })),
      deleteNote: vi.fn().mockReturnValue(of(void 0)),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(confirmStatus)) };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'lead-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: LeadStore, useValue: {} },
        { provide: LeadService, useValue: leadServiceMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new LeadDetailComponent());
    comp.ngOnInit();
    return { comp, leadServiceMock, toasterMock };
  }

  it('edits an existing CRM note and clears edit state', () => {
    const { comp, leadServiceMock } = createComponent();
    const note = comp.notes()[0];

    comp.startEditNote(note);
    expect(comp.editingNoteId()).toBe('note-1');
    expect(comp.editingNoteText()).toBe('Initial call completed');

    comp.editingNoteText.set('  Follow-up scheduled  ');
    comp.saveEditNote('note-1');

    expect(leadServiceMock.editNote).toHaveBeenCalledWith('lead-1', 'note-1', { noteText: 'Follow-up scheduled' });
    expect(comp.editingNoteId()).toBeNull();
    expect(comp.editingNoteText()).toBe('');
  });

  it('deletes a note only after confirmation', () => {
    const cancelled = createComponent(Confirmation.Status.dismiss);
    cancelled.comp.deleteNote('note-1');
    expect(cancelled.leadServiceMock.deleteNote).not.toHaveBeenCalled();

    const confirmed = createComponent(Confirmation.Status.confirm);
    confirmed.comp.deleteNote('note-1');
    expect(confirmed.leadServiceMock.deleteNote).toHaveBeenCalledWith('lead-1', 'note-1');
    expect(confirmed.toasterMock.success).toHaveBeenCalledWith('::SuccessfullyDeleted');
  });
});
