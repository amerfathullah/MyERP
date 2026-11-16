import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { InstallationNoteDetailComponent } from './installation-note-detail.component';
import { InstallationNoteService } from '../../proxy/sales/installation-note.service';

describe('InstallationNoteDetailComponent error handling', () => {
  it('surfaces toaster error when load, submit, or cancel fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Note load failed' } } }))),
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Submit blocked' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Cancel blocked' } } }))),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'in-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: InstallationNoteService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new InstallationNoteDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Note load failed');

    comp.note = { id: 'in-1', status: 0 };
    comp.submit();
    expect(toasterMock.error).toHaveBeenCalledWith('Submit blocked');

    comp.cancel();
    expect(toasterMock.error).toHaveBeenCalledWith('Cancel blocked');
  });
});
