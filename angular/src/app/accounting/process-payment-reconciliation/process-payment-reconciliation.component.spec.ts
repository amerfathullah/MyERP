import { Injector, runInInjectionContext } from '@angular/core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { ProcessPaymentReconciliationComponent } from './process-payment-reconciliation.component';
import { ProcessPaymentReconciliationService } from '../../proxy/accounting/process-payment-reconciliation.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('ProcessPaymentReconciliationComponent', () => {
  it('updates row progress and status via ProcessPaymentReconciliationService.getProgress', () => {
    const serviceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
      getProgress: vi.fn().mockReturnValue(of({
        id: 'ppr-1',
        status: 3,
        statusName: 'Completed',
        reconciledCount: 12,
        errorLog: null,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: ProcessPaymentReconciliationService, useValue: serviceMock },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn() } },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ProcessPaymentReconciliationComponent());
    comp.items = [{ id: 'ppr-1', partyType: 'Customer', partyId: 'c-1', status: 2, reconciledCount: 4 } as any];

    comp.refresh(comp.items[0]);

    expect(serviceMock.getProgress).toHaveBeenCalledWith('ppr-1');
    expect(comp.items[0].status).toBe(3);
    expect(comp.items[0].reconciledCount).toBe(12);
  });
});
