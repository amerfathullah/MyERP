import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ShippingRuleDetailComponent } from './shipping-rule-detail.component';
import { ShippingRuleService } from '../../proxy/sales/shipping-rule.service';

describe('ShippingRuleDetailComponent error handling', () => {
  it('surfaces toaster error when load fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Shipping rule load failed' } } }))),
    };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'sr-1' } } } },
        { provide: ShippingRuleService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ShippingRuleDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Shipping rule load failed');
  });
});
