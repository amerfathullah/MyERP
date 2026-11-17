import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { DunningDetailComponent } from './dunning-detail.component';
import { DunningFormComponent } from './dunning-form.component';
import { DunningService } from '../../proxy/sales/dunning.service';
import { DunningTypeService } from '../../proxy/sales/dunning-type.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { PaymentReconciliationService } from '../../proxy/accounting/payment-reconciliation.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

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

  it('surfaces toaster error when loading dunning or overdue payments fails', () => {
    const dunningServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'dun-1', status: 1, customerName: 'Acme' })),
      getUnpaidOverduePayments: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Overdue query failed' } } }))),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'dun-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: DunningService, useValue: dunningServiceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new DunningDetailComponent());
    comp.ngOnInit();

    expect(comp.overduePayments()).toEqual([]);
    expect(toasterMock.error).toHaveBeenCalledWith('Overdue query failed');
  });
});

describe('DunningFormComponent Overdue Mapping & Error Handling', () => {
  function setupForm(createResult$: any) {
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const pastDate = new Date(Date.now() - 10 * 86400000).toISOString().split('T')[0];
    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: DunningService, useValue: { create: vi.fn().mockReturnValue(createResult$) } },
        { provide: DunningTypeService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        {
          provide: PaymentReconciliationService,
          useValue: {
            getOutstandingInvoices: vi.fn().mockReturnValue(of([
              { voucherId: 'si-10', voucherNumber: 'SI-010', postingDate: pastDate, invoicedAmount: 250, outstanding: 150 },
            ])),
          },
        },
        { provide: ToasterService, useValue: toasterMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });
    const comp = runInInjectionContext(injector, () => new DunningFormComponent());
    return { comp, toasterMock };
  }

  it('maps OutstandingInvoiceDto voucherId and outstanding fields on fetchOverdueInvoices', () => {
    const { comp } = setupForm(of({ id: 'd-1' }));
    comp.form.patchValue({ customerId: 'cust-1' });
    comp.fetchOverdueInvoices();

    expect(comp.overdueInvoices()).toHaveLength(1);
    expect(comp.overdueInvoices()[0].id).toBe('si-10');
    expect(comp.overdueInvoices()[0].outstandingAmount).toBe(150);
    expect(comp.totalOutstanding()).toBe(150);
  });

  it('surfaces backend error via toaster when save fails', () => {
    const { comp, toasterMock } = setupForm(throwError(() => ({ error: { error: { message: 'Dunning failed' } } })));
    comp.form.patchValue({ customerId: 'cust-1' });
    comp.fetchOverdueInvoices();
    comp.save();

    expect(comp.saving).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Dunning failed');
  });
});
