import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { SalesPersonDetailComponent } from './sales-person-detail.component';
import { SalesPersonService } from '../../proxy/sales/sales-person.service';

describe('SalesPersonDetailComponent', () => {
  let serviceMock: any;

  function createComponent(): SalesPersonDetailComponent {
    const injector = Injector.create({
      providers: [
        { provide: SalesPersonService, useValue: serviceMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'sp-1' } } } },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });
    return runInInjectionContext(injector, () => new SalesPersonDetailComponent());
  }

  beforeEach(() => {
    serviceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'sp-1', name: 'Ali Ahmad', commissionRate: 5, targets: [] })),
      getTimelineData: vi.fn().mockReturnValue(of({ '2026-10-01': 2, '2026-11-15': 5 })),
    };
  });

  it('loads timeline activity sorted newest-first and totals transactions', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(serviceMock.get).toHaveBeenCalledWith('sp-1');
    expect(serviceMock.getTimelineData).toHaveBeenCalledWith('sp-1');
    expect(comp.timelineEntries()).toEqual([
      { date: '2026-11-15', count: 5 },
      { date: '2026-10-01', count: 2 },
    ]);
    expect(comp.totalTimelineTransactions()).toBe(7);
  });

  it('resets timelineEntries gracefully on error', () => {
    serviceMock.getTimelineData.mockReturnValue(throwError(() => new Error('err')));
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.timelineEntries()).toEqual([]);
    expect(comp.totalTimelineTransactions()).toBe(0);
  });
});
