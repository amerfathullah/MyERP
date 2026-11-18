import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import { SalesRegisterComponent, buildItemWiseSalesCsvRows } from './sales-register.component';
import { SalesRegisterService } from '../../../proxy/sales/sales-register.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import { CustomerGroupService } from '../../../proxy/core/customer-group.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { ItemWiseSalesRegisterLineDto } from '../../../proxy/sales/models';

describe('SalesRegisterComponent', () => {
  it('formats item-wise sales register rows for CSV export', () => {
    const items: ItemWiseSalesRegisterLineDto[] = [
      {
        invoiceNumber: 'SINV-001',
        postingDate: '2026-11-18',
        customerName: 'Acme Corp',
        customerGroupName: 'Enterprise',
        itemCode: 'SKU-100',
        itemName: 'Widget Pro',
        itemGroup: 'Electronics',
        stockQty: 5,
        stockUom: 'Unit',
        rate: 40,
        amount: 200,
        totalTax: 12,
        total: 212,
        salesOrderNumber: 'SO-001',
        deliveryNoteNumber: 'DN-001',
      },
    ];

    const csv = buildItemWiseSalesCsvRows(items);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Customer']).toBe('Acme Corp');
    expect(csv[0]['Item Code']).toBe('SKU-100');
    expect(csv[0]['Total']).toBe('212.00');
    expect(csv[0]['SO Number']).toBe('SO-001');
    expect(csv[0]['DN Number']).toBe('DN-001');
  });

  it('calls getItemWiseReport with customerGroupId and itemCode when switched to item mode', () => {
    const registerMock = {
      getReport: vi.fn().mockReturnValue(of({ count: 0, items: [] })),
      getItemWiseReport: vi.fn().mockReturnValue(of({ count: 1, items: [{ invoiceNumber: 'SINV-001' }] })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: SalesRegisterService, useValue: registerMock },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CustomerGroupService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Main' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SalesRegisterComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ customerGroupId: 'cg-1', itemCode: ' SKU-100 ' });
    comp.switchMode('item');

    expect(registerMock.getItemWiseReport).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      customerGroupId: 'cg-1',
      itemCode: 'SKU-100',
    }));
    expect(comp.itemWiseReport()?.count).toBe(1);
  });
});
