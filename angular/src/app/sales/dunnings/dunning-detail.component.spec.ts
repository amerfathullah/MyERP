import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { DunningDetailComponent } from './dunning-detail.component';
import { DunningService } from '../../proxy/sales/dunning.service';

describe('DunningDetailComponent Unpaid Overdue Payments', () => {
  function createComponent() {
    const dunningServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'dun-1', status: 1, customerName: 'Acme' })),
      getUnpaidOverduePayments: vi.fn().mockReturnValue(of([
        { salesInvoiceId: 'si-1', invoiceNumber: 'SI-001', overdueDays: 14, outstandingAmount: 500 },
      ])),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'dun-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: DunningService, useValue: dunningServiceMock },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new DunningDetailComponent());
    comp.ngOnInit();
    return { comp, dunningServiceMock };
  }

  it('fetches live unpaid overdue payments and reloads on currency toggle', () => {
    const { comp, dunningServiceMock } = createComponent();
    expect(dunningServiceMock.getUnpaidOverduePayments).toHaveBeenCalledWith('dun-1', false);
    expect(comp.overduePayments()).toHaveLength(1);

    comp.toggleAccountCurrency(true);
    expect(dunningServiceMock.getUnpaidOverduePayments).toHaveBeenCalledWith('dun-1', true);
  });
});
