import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { HolidayListDetailComponent } from './holiday-list-detail.component';
import { HolidayListDetailService } from '../../shared/services/detail-services';

describe('HolidayListDetailComponent error handling', () => {
  it('surfaces toaster error when load fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Holiday list load failed' } } }))),
    };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'hl-1' } } } },
        { provide: HolidayListDetailService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new HolidayListDetailComponent());
    comp.ngOnInit();
    expect(toasterMock.error).toHaveBeenCalledWith('Holiday list load failed');
  });
});
