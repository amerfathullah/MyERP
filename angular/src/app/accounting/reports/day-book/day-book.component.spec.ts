import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DayBookComponent, formatDayBookVoucherDate } from './day-book.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('DayBookComponent', () => {
  let reportingMock: { getDayBook: ReturnType<typeof vi.fn> };
  let companyMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: DayBookComponent;

  beforeEach(() => {
    reportingMock = {
      getDayBook: vi.fn().mockReturnValue(of({
        companyId: 'comp-1',
        voucherCount: 1,
        totalDebit: 250,
        totalCredit: 0,
        rows: [
          {
            voucherKey: 'SalesInvoice::si-1',
            indent: 0,
            postingDate: '2026-11-18T00:00:00Z',
            particulars: 'Acme Customer',
            voucherType: 'SalesInvoice',
            voucherId: 'si-1',
            voucherNo: 'SI-0001',
            debit: 250,
            credit: null,
          },
          {
            voucherKey: 'SalesInvoice::si-1::0',
            parentVoucherKey: 'SalesInvoice::si-1',
            indent: 1,
            particulars: 'Acme Customer',
            account: '1200 - Debtors',
            debit: 250,
            credit: null,
          },
        ],
      })),
    };
    companyMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Test Co' }] })),
    };
    routerMock = {
      navigate: vi.fn(),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: routerMock },
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new DayBookComponent());
  });

  it('loads default company and requests day book report', () => {
    comp.ngOnInit();

    expect(reportingMock.getDayBook).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      detailed: false,
      showRemarks: false,
    }));
    expect(comp.summaryCount()).toBe(1);
    expect(comp.rows().length).toBe(2);
    expect(formatDayBookVoucherDate('2026-11-18T10:30:00Z')).toBe('2026-11-18');
  });

  it('drills down only from summary voucher rows to general ledger', () => {
    comp.filters.patchValue({ companyId: 'comp-1', fromDate: '2026-11-01', toDate: '2026-11-18' });

    comp.drillDown({ voucherKey: 'leg-1', indent: 1, particulars: 'Sales', voucherNo: undefined });
    expect(routerMock.navigate).not.toHaveBeenCalled();

    comp.drillDown({ voucherKey: 'SalesInvoice::si-1', indent: 0, particulars: 'Acme', voucherNo: 'SI-0001' });
    expect(routerMock.navigate).toHaveBeenCalledWith(
      ['/accounting/reports/general-ledger'],
      {
        queryParams: {
          companyId: 'comp-1',
          fromDate: '2026-11-01',
          toDate: '2026-11-18',
          voucherNumber: 'SI-0001',
        },
      },
    );
  });
});
