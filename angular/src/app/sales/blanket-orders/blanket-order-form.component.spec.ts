import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { BlanketOrderFormComponent } from './blanket-order-form.component';
import { BlanketOrderService } from '../../proxy/sales/blanket-order.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';

describe('BlanketOrderFormComponent multi-currency support', () => {
  it('defaults currency from CompanyContextService and sends currency and exchangeRate on save', () => {
    const createMock = vi.fn().mockReturnValue(of({ id: 'bo-1' }));
    const routerMock = { navigate: vi.fn() };
    const companyContextMock = {
      currentCompanyId: signal('comp-1'),
      currentCurrency: signal('USD'),
    };
    const injector = Injector.create({
      providers: [
        { provide: BlanketOrderService, useValue: { create: createMock } },
        { provide: Router, useValue: routerMock },
        { provide: CompanyContextService, useValue: companyContextMock },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: SupplierService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new BlanketOrderFormComponent());
    expect(comp.form.currency).toBe('USD');
    expect(comp.form.exchangeRate).toBe(1);

    comp.onCurrencyChange(' eur ');
    expect(comp.form.currency).toBe('EUR');
    comp.form.exchangeRate = 1.1;
    comp.form.partyId = 'cust-1';
    comp.form.fromDate = '2026-01-01';
    comp.form.toDate = '2026-12-31';
    comp.form.items = [{ itemId: 'item-1', qty: 10, rate: 50, stockUom: 'Unit' }];

    comp.save();

    expect(createMock).toHaveBeenCalledWith(
      expect.objectContaining({
        companyId: 'comp-1',
        currency: 'EUR',
        exchangeRate: 1.1,
      }),
    );

    comp.onCurrencyChange('usd');
    expect(comp.form.currency).toBe('USD');
    expect(comp.form.exchangeRate).toBe(1);
  });
});
