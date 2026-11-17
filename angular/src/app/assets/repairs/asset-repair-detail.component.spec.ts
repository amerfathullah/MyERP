import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { AssetRepairDetailComponent } from './asset-repair-detail.component';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';

describe('AssetRepairDetailComponent error handling', () => {
  it('surfaces toaster error when load, complete, or cancel fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Repair load failed' } } }))),
      complete: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Repair complete failed' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Repair cancel failed' } } }))),
    };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ar-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: AssetRepairService, useValue: serviceMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new AssetRepairDetailComponent());
    comp.ngOnInit();
    expect(toasterMock.error).toHaveBeenCalledWith('Repair load failed');

    comp.repair.set({ id: 'ar-1', status: 0 } as any);
    comp.onAction('complete');
    expect(toasterMock.error).toHaveBeenCalledWith('Repair complete failed');

    comp.onAction('cancel');
    expect(toasterMock.error).toHaveBeenCalledWith('Repair cancel failed');
  });
});
