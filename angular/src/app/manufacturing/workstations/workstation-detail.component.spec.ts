import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { WorkstationDetailComponent } from './workstation-detail.component';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';

describe('WorkstationDetailComponent error handling', () => {
  it('surfaces toaster error when load fails', () => {
    const serviceMock = {
      getWorkstation: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Workstation load failed' } } }))),
    };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ws-1' } } } },
        { provide: ManufacturingService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new WorkstationDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Workstation load failed');
  });
});
