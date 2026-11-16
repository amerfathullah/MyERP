import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PaymentRequestDetailComponent } from './payment-request-detail.component';
import { PaymentRequestService } from '../../proxy/accounting/payment-request.service';

describe('PaymentRequestDetailComponent Subscription Details', () => {
  function createComponent(isSubscription: boolean) {
    const prServiceMock = {
      get: vi.fn().mockReturnValue(of({
        id: 'pr-1',
        status: 1,
        isASubscription: isSubscription,
        referenceDoctype: 'SalesOrder',
        referenceId: 'so-9',
        grandTotal: 250,
      })),
      getSubscriptionDetails: vi.fn().mockReturnValue(of([
        { planId: 'plan-1', itemName: 'Cloud License', qty: 2, rate: 125, amount: 250 },
      ])),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'pr-1' } } } },
        { provide: PaymentRequestService, useValue: prServiceMock },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new PaymentRequestDetailComponent());
    comp.ngOnInit();
    return { comp, prServiceMock };
  }

  it('fetches subscription plan breakdown when PaymentRequest is a subscription', () => {
    const { comp, prServiceMock } = createComponent(true);
    expect(prServiceMock.getSubscriptionDetails).toHaveBeenCalledWith('SalesOrder', 'so-9');
    expect(comp.subscriptionPlans()).toHaveLength(1);
    expect(comp.subscriptionPlans()[0].itemName).toBe('Cloud License');
  });

  it('skips getSubscriptionDetails when PaymentRequest is not a subscription', () => {
    const { comp, prServiceMock } = createComponent(false);
    expect(prServiceMock.getSubscriptionDetails).not.toHaveBeenCalled();
    expect(comp.subscriptionPlans()).toEqual([]);
  });
});
