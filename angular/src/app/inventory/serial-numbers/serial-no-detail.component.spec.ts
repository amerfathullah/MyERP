import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SerialNoDetailComponent } from './serial-no-detail.component';
import { SerialNoService } from '../../proxy/inventory/serial-no.service';

describe('SerialNoDetailComponent error handling', () => {
  it('surfaces toaster error and navigates back when load fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Serial load failed' } } }))),
    };
    const routerMock = { navigate: vi.fn() };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'sn-1' } } } },
        { provide: Router, useValue: routerMock },
        { provide: SerialNoService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SerialNoDetailComponent());
    comp.ngOnInit();
    expect(toasterMock.error).toHaveBeenCalledWith('Serial load failed');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/inventory/serial-numbers']);
  });
});
