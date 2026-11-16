import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { LoyaltyProgramDetailComponent } from './loyalty-program-detail.component';
import { LoyaltyProgramService } from '../../proxy/sales/loyalty-program.service';
import { CustomerService } from '../../proxy/sales/customer.service';

describe('LoyaltyProgramDetailComponent error handling', () => {
  it('surfaces toaster error when program, customers, or history fail to load', () => {
    const lpMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Program load failed' } } }))),
      getCustomerBalance: vi.fn().mockReturnValue(of(null)),
      getPointHistory: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'History load failed' } } }))),
    };
    const customerMock = {
      getList: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Customer list failed' } } }))),
    };
    const toasterMock = { success: vi.fn(), warn: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: LoyaltyProgramService, useValue: lpMock },
        { provide: CustomerService, useValue: customerMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'lp-1' } } } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new LoyaltyProgramDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Program load failed');
    expect(toasterMock.error).toHaveBeenCalledWith('Customer list failed');

    comp.program = { id: 'lp-1' };
    comp.selectedCustomerId = 'cust-1';
    comp.loadPointHistory();
    expect(toasterMock.error).toHaveBeenCalledWith('History load failed');
  });
});
