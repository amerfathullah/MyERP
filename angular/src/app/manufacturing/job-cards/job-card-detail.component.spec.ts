import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { JobCardDetailComponent } from './job-card-detail.component';
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

  it('clears rawMaterials gracefully if getRawMaterials fails', () => {
    serviceMock.getRawMaterials.mockReturnValue(throwError(() => new Error('err')));
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.rawMaterials()).toEqual([]);
  });
});
