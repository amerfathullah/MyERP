import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PosClosingDetailComponent } from './pos-closing-detail.component';
import { PosClosingService } from '../../proxy/sales/pos-closing.service';

describe('PosClosingDetailComponent error handling', () => {
  it('surfaces toaster error when submit or cancel fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'pc-1', status: 'Draft' })),
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Closing submit failed' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Closing cancel failed' } } }))),
      retry: vi.fn().mockReturnValue(of({})),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'pc-1' } } } },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: PosClosingService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new PosClosingDetailComponent());
    comp.ngOnInit();

    comp.submit();
    expect(toasterMock.error).toHaveBeenCalledWith('Closing submit failed');

    comp.cancelEntry();
    expect(toasterMock.error).toHaveBeenCalledWith('Closing cancel failed');
  });
});
