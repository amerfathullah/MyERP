import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { OpportunityDetailComponent } from './opportunity-detail.component';
import { OpportunityService } from '../../proxy/crm/opportunity.service';
import { CompetitorService } from '../../proxy/crm/competitor.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';

describe('OpportunityDetailComponent Stages & Notes', () => {
  function createComponent(confirmStatus = Confirmation.Status.confirm) {
    const oppServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'opp-1', title: 'Big Deal', status: 0 })),
      getNotes: vi.fn().mockReturnValue(of([{ id: 'n-1', noteText: 'Initial discovery', addedOn: '2026-11-16T08:00:00Z' }])),
      addNote: vi.fn().mockReturnValue(of({ id: 'n-2', noteText: 'Sent deck' })),
      editNote: vi.fn().mockReturnValue(of({ id: 'n-1', noteText: 'Updated note' })),
      deleteNote: vi.fn().mockReturnValue(of(void 0)),
      markReplied: vi.fn().mockReturnValue(of({ id: 'opp-1', status: 1 })),
      markQuotation: vi.fn().mockReturnValue(of({ id: 'opp-1', status: 2 })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(confirmStatus)) };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'opp-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: OpportunityService, useValue: oppServiceMock },
        { provide: CompetitorService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: DocumentConversionService, useValue: {} },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new OpportunityDetailComponent());
    comp.ngOnInit();
    return { comp, oppServiceMock, toasterMock };
  }

  it('transitions opportunity through markReplied and markQuotation', () => {
    const { comp, oppServiceMock, toasterMock } = createComponent();

    comp.markReplied();
    expect(oppServiceMock.markReplied).toHaveBeenCalledWith('opp-1');
    expect(comp.opp()?.status).toBe(1);

    comp.markQuotation();
    expect(oppServiceMock.markQuotation).toHaveBeenCalledWith('opp-1');
    expect(comp.opp()?.status).toBe(2);
    expect(toasterMock.success).toHaveBeenCalledTimes(2);
  });

  it('supports adding, editing, and confirmed deleting of notes', () => {
    const { comp, oppServiceMock } = createComponent();
    expect(comp.notes()).toHaveLength(1);

    comp.newNoteText.set('  Sent deck  ');
    comp.addNote();
    expect(oppServiceMock.addNote).toHaveBeenCalledWith('opp-1', { noteText: 'Sent deck' });

    comp.startEditNote(comp.notes()[0]);
    comp.editingNoteText.set('Updated discovery');
    comp.saveEditNote('n-1');
    expect(oppServiceMock.editNote).toHaveBeenCalledWith('opp-1', 'n-1', { noteText: 'Updated discovery' });

    comp.deleteNote('n-1');
    expect(oppServiceMock.deleteNote).toHaveBeenCalledWith('opp-1', 'n-1');
  });
});
