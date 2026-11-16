import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { QualityInspectionDetailComponent } from './quality-inspection-detail.component';
import { QualityInspectionDetailService } from '../../shared/services/detail-services';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';

describe('QualityInspectionDetailComponent error handling', () => {
  it('surfaces toaster error when load, submit, or cancel fails', () => {
    const detailMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'QI load failed' } } }))),
    };
    const qiMock = {
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'QI submit failed' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'QI cancel failed' } } }))),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'qi-1' } } } },
        { provide: QualityInspectionDetailService, useValue: detailMock },
        { provide: QualityInspectionService, useValue: qiMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QualityInspectionDetailComponent());
    comp.ngOnInit();
    expect(toasterMock.error).toHaveBeenCalledWith('QI load failed');

    comp.submit();
    expect(comp.acting).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('QI submit failed');

    comp.cancel();
    expect(comp.acting).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('QI cancel failed');
  });
});
