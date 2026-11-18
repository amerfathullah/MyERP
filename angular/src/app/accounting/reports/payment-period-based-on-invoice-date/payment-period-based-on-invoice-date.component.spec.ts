import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentPeriodBasedOnInvoiceDateComponent } from './payment-period-based-on-invoice-date.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('PaymentPeriodBasedOnInvoiceDateComponent', () => {
  let reportingMock: { getPaymentPeriodBasedOnInvoiceDate: ReturnType<typeof vi.fn> };
  let companyMock: { getList: ReturnType<typeof vi.fn> };
  let customerMock: { getList: ReturnType<typeof vi.fn> };
  let supplierMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: PaymentPeriodBasedOnInvoiceDateComponent;

  beforeEach(() => {
    reportingMock = {
      getPaymentPeriodBasedOnInvoiceDate: vi.fn().mockReturnValue(of({
        companyId: 'comp-1',
        paymentType: 'Incoming',
        currencyCode: 'MYR',
        totalAmount: 1000,
        totalRange0To30: 0,
        totalRange31To60: 1000,
        totalRange61To90: 0,
        totalRange90Above: 0,
        rows: [
          {
            paymentDocumentType: 'PaymentEntry',
            paymentDocumentId: 'pe-1',
            paymentDocumentNo: 'PE-0001',
            partyType: 'Customer',
            partyId: 'cust-1',
            partyName: 'Acme Trading',
            postingDate: '2026-11-18T00:00:00Z',
            invoiceType: 'SalesInvoice',
            invoiceId: 'si-1',
            invoiceNo: 'SI-0001',
            invoicePostingDate: '2026-10-01T00:00:00Z',
            dueDate: '2026-10-31T00:00:00Z',
            amount: 1000,
            age: 48,
            range31To60: 1000,
            delayInPaymentDays: 18,
          },
        ],
      })),
    };
    companyMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Test Co' }] })) };
    customerMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'cust-1', name: 'Acme Trading' }] })) };
    supplierMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sup-1', name: 'Vendor Sdn Bhd' }] })) };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: routerMock },
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CustomerService, useValue: customerMock },
        { provide: SupplierService, useValue: supplierMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new PaymentPeriodBasedOnInvoiceDateComponent());
  });

  it('loads incoming customers and generates payment period ageing report', () => {
    comp.ngOnInit();

    expect(customerMock.getList).toHaveBeenCalled();
    expect(reportingMock.getPaymentPeriodBasedOnInvoiceDate).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      paymentType: 'Incoming',
    }));
    expect(comp.rows().length).toBe(1);
    expect(comp.parties().map(p => p.id)).toEqual(['cust-1']);
  });

  it('switches to suppliers on Outgoing and drills down to payment or invoice', () => {
    comp.ngOnInit();
    comp.filters.patchValue({ paymentType: 'Outgoing' });
    comp.onPaymentTypeChange();

    expect(supplierMock.getList).toHaveBeenCalled();
    expect(comp.parties().map(p => p.id)).toEqual(['sup-1']);

    const row = { paymentDocumentType: 'PaymentEntry', paymentDocumentId: 'pe-1', invoiceType: 'SalesInvoice', invoiceId: 'si-1' };
    comp.drillDownPayment(row);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/payments', 'pe-1']);

    comp.drillDownInvoice(row, { stopPropagation: vi.fn() } as unknown as Event);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/invoices', 'si-1']);
  });
});
