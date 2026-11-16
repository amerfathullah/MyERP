import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SupplierScorecardDetailComponent } from './supplier-scorecard-detail.component';
import { SupplierScorecardService } from '../../proxy/purchasing/supplier-scorecard.service';

describe('SupplierScorecardDetailComponent Score Override', () => {
  function createComponent() {
    const initial = { id: 'sc-1', supplierName: 'ABC Supplies', score: 65, periodType: 'Monthly', standings: [], criteria: [] };
    const updated = { ...initial, score: 88.5 };
    const scorecardServiceMock = {
      get: vi.fn().mockReturnValue(of(initial)),
      updateScore: vi.fn().mockReturnValue(of(updated)),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: SupplierScorecardService, useValue: scorecardServiceMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'sc-1' } } } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SupplierScorecardDetailComponent());
    comp.ngOnInit();
    return { comp, scorecardServiceMock, toasterMock };
  }

  it('opens override input prefilled with current score and updates via updateScore API', () => {
    const { comp, scorecardServiceMock, toasterMock } = createComponent();

    comp.openScoreOverride();
    expect(comp.showScoreOverride).toBe(true);
    expect(comp.overrideScore).toBe(65);

    comp.overrideScore = 88.5;
    comp.saveScoreOverride();

    expect(scorecardServiceMock.updateScore).toHaveBeenCalledWith('sc-1', 88.5);
    expect(comp.scorecard.score).toBe(88.5);
    expect(comp.showScoreOverride).toBe(false);
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
  });
});
