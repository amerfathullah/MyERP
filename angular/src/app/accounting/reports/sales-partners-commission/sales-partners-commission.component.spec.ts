import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import {
  SalesPartnersCommissionComponent,
  buildSalesPartnersCommissionCsvRows,
} from './sales-partners-commission.component';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('SalesPartnersCommissionComponent', () => {
  it('loads aggregated partner commissions and exports CSV rows', () => {
    const sampleReport = {
      companyId: 'c1',
      currencyCode: 'MYR',
      totalInvoicedAmount: 400,
      totalEligibleAmount: 400,
      totalCommission: 40,
      rows: [
        {
          salesPartnerId: 'sp-1',
          salesPartnerName: 'Partner Alpha',
          currencyCode: 'MYR',
          invoiceCount: 3,
          invoicedAmount: 400,
          amountEligibleForCommission: 400,
          totalCommission: 40,
          averageCommissionRate: 10,
        },
      ],
    };

    const reportingMock = {
      getSalesPartnersCommission: vi.fn().mockReturnValue(of(sampleReport)),
    };
    const companyMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'ACME Bhd' }] })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn(), success: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SalesPartnersCommissionComponent());
    comp.ngOnInit();

    expect(reportingMock.getSalesPartnersCommission).toHaveBeenCalledWith(
      expect.objectContaining({ companyId: 'c1' }),
    );
    expect(comp.report()?.totalCommission).toBe(40);

    const csv = buildSalesPartnersCommissionCsvRows(sampleReport);
    expect(csv[0]['Sales Partner']).toBe('Partner Alpha');
    expect(csv[0]['Total Commission']).toBe('40.00');
    expect(csv[0]['Average Commission Rate (%)']).toBe('10.00');
  });
});
