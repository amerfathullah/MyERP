import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { CouponCodeListComponent } from './coupon-code-list.component';
import { CouponCodeService } from '../../proxy/sales/coupon-code.service';
import { PricingRuleService } from '../../proxy/sales/pricing-rule.service';
import { CustomerService } from '../../proxy/sales/customer.service';

describe('CouponCodeListComponent', () => {
  let couponMock: any;
  let toasterMock: any;

  function createComponent(): CouponCodeListComponent {
    const injector = Injector.create({
      providers: [
        { provide: CouponCodeService, useValue: couponMock },
        { provide: PricingRuleService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
      ],
    });
    return runInInjectionContext(injector, () => new CouponCodeListComponent());
  }

  beforeEach(() => {
    couponMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'cp-1', code: 'SAVE10', used: 2 }], totalCount: 1 })),
      reverseUsage: vi.fn().mockReturnValue(of(void 0)),
      validateAndApply: vi.fn().mockReturnValue(of('"pr-rule-1"')),
    };
    toasterMock = { success: vi.fn(), error: vi.fn() };
  });

  it('reverses coupon usage and reloads list', () => {
    const comp = createComponent();
    comp.reverseUsage({ id: 'cp-1', code: 'SAVE10', used: 2 });

    expect(couponMock.reverseUsage).toHaveBeenCalledWith('cp-1');
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
    expect(couponMock.getList).toHaveBeenCalledTimes(1);
  });

  it('validates and applies coupon for customer/date and captures pricingRuleId', () => {
    const comp = createComponent();
    comp.testCouponCode = ' SAVE10 ';
    comp.testCustomerId = 'cust-1';
    comp.testDate = '2026-11-16';

    comp.validateAndApply();

    expect(couponMock.validateAndApply).toHaveBeenCalledWith('SAVE10', 'cust-1', '2026-11-16');
    expect(comp.appliedPricingRuleId()).toBe('pr-rule-1');
    expect(comp.validatingCoupon()).toBe(false);
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
  });

  it('surfaces backend error detail and clears validating flag on invalid coupon', () => {
    couponMock.validateAndApply.mockReturnValue(throwError(() => ({ error: { error: { data: { detail: 'Coupon expired' } } } })));
    const comp = createComponent();
    comp.testCouponCode = 'OLD';
    comp.testCustomerId = 'cust-1';

    comp.validateAndApply();

    expect(comp.validatingCoupon()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Coupon expired');
  });
});
