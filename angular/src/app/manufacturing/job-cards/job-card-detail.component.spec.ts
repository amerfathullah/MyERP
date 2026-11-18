import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { JobCardDetailComponent, computeJobCardElapsedSeconds } from './job-card-detail.component';
import { JobCardService } from '../../proxy/manufacturing/job-card.service';

describe('JobCardDetailComponent', () => {
  let serviceMock: any;

  function createComponent(): JobCardDetailComponent {
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'jc-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: JobCardService, useValue: serviceMock },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });
    return runInInjectionContext(injector, () => new JobCardDetailComponent());
  }

  beforeEach(() => {
    serviceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'jc-1', sequenceId: 1, forQuantity: 10, completedQty: 2, status: 0 })),
      getRawMaterials: vi.fn().mockReturnValue(of([
        { itemId: 'rm-1', itemCode: 'RM-01', itemName: 'Steel Sheet', requiredQty: 20, transferredQty: 10, pendingQty: 10, stockQty: 25, isAvailable: true },
        { itemId: 'rm-2', itemCode: 'RM-02', itemName: 'Bolt', requiredQty: 40, transferredQty: 0, pendingQty: 40, stockQty: 12, isAvailable: false },
      ])),
    };
  });

  it('loads job card details and raw materials on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(serviceMock.get).toHaveBeenCalledWith('jc-1');
    expect(serviceMock.getRawMaterials).toHaveBeenCalledWith('jc-1');
    expect(comp.rawMaterials()).toHaveLength(2);
    expect(comp.rawMaterials()[0].isAvailable).toBe(true);
    expect(comp.rawMaterials()[1].isAvailable).toBe(false);
  });

  it('clears rawMaterials and surfaces toaster error when loading fails', () => {
    serviceMock.get.mockReturnValue(throwError(() => ({ error: { error: { message: 'JC load failed' } } })));
    serviceMock.getRawMaterials.mockReturnValue(throwError(() => ({ error: { error: { message: 'RM load failed' } } })));
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'jc-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: JobCardService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });
    const comp = runInInjectionContext(injector, () => new JobCardDetailComponent());
    comp.ngOnInit();

    expect(comp.rawMaterials()).toEqual([]);
    expect(toasterMock.error).toHaveBeenCalledWith('JC load failed');
    expect(toasterMock.error).toHaveBeenCalledWith('RM load failed');
  });

  it('computes union of overlapping UTC intervals with fractional seconds and resumes WIP timer on load', () => {
    // Two overlapping operator logs: 08:00:00.000..08:00:10.500 and 08:00:05.000..08:00:15.250 -> 15.25s
    const elapsed = computeJobCardElapsedSeconds(
      [
        { fromTime: '2026-11-18T08:00:00.000', toTime: '2026-11-18T08:00:10.500' },
        { fromTime: '2026-11-18T08:00:05.000', toTime: '2026-11-18T08:00:15.250' },
      ],
      Date.parse('2026-11-18T08:00:20.000Z'),
    );
    expect(elapsed).toBeCloseTo(15.25, 2);

    // Closed log ending after now does not grow past its end when recomputed on tick
    const capped = computeJobCardElapsedSeconds(
      [{ fromTime: '2026-11-18T08:00:00Z', toTime: '2026-11-18T08:00:30Z' }],
      Date.parse('2026-11-18T08:00:10Z'),
    );
    expect(capped).toBe(30);

    // Reloading a WorkInProgress Job Card resumes live timer from startedAt
    vi.useFakeTimers();
    try {
      vi.setSystemTime(new Date('2026-11-18T08:01:05Z'));
      serviceMock.get.mockReturnValue(of({
        id: 'jc-1',
        sequenceId: 1,
        forQuantity: 10,
        completedQty: 0,
        status: 1,
        startedAt: '2026-11-18T08:00:00Z',
        timeLogs: [],
      }));

      const comp = createComponent();
      comp.ngOnInit();
      expect(comp.timerRunning()).toBe(true);
      expect(comp.timerDisplay()).toBe('00:01:05');

      vi.advanceTimersByTime(2000);
      expect(comp.timerDisplay()).toBe('00:01:07');
      comp.ngOnDestroy();
    } finally {
      vi.useRealTimers();
    }
  });
});
