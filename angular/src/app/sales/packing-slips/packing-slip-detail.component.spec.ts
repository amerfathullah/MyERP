import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PackingSlipDetailComponent } from './packing-slip-detail.component';
import { PackingSlipService } from '../../proxy/sales/packing-slip.service';

describe('PackingSlipDetailComponent error handling', () => {
  it('surfaces toaster error when load, submit, cancel, or delete fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Slip load failed' } } }))),
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Slip submit failed' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Slip cancel failed' } } }))),
      delete: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Slip delete failed' } } }))),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ps-1' } } } },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
        { provide: PackingSlipService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new PackingSlipDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Slip load failed');

    comp.submit();
    expect(toasterMock.error).toHaveBeenCalledWith('Slip submit failed');

    comp.cancelSlip();
    expect(toasterMock.error).toHaveBeenCalledWith('Slip cancel failed');

    comp.deleteSlip();
    expect(toasterMock.error).toHaveBeenCalledWith('Slip delete failed');
  });
});
