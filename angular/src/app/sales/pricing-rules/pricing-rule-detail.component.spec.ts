import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PricingRuleDetailComponent } from './pricing-rule-detail.component';
import { PricingRuleService } from '../../proxy/sales/pricing-rule.service';

describe('PricingRuleDetailComponent error handling', () => {
  it('surfaces toaster error when load fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Pricing rule load failed' } } }))),
    };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'pr-1' } } } },
        { provide: PricingRuleService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new PricingRuleDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Pricing rule load failed');
  });
});
