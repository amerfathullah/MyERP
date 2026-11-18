import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  TaxWithholdingDetailsComponent,
  buildTaxWithholdingDetailsCsvRows,
} from './tax-withholding-details.component';
import { TaxWithholdingReportService } from '../../proxy/tax/tax-withholding-report.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('TaxWithholdingDetailsComponent', () => {
  it('formats tax withholding detail rows for CSV export', () => {
    const csv = buildTaxWithholdingDetailsCsvRows([
      {
        postingDate: '2026-11-18',
        partyType: 'Supplier',
        partyName: 'NonResident Tech',
        tin: 'C123456',
        taxCategory: 'Section 107A',
        rate: 10,
        taxableAmount: 1000,
        withheldAmount: 100,
        voucherType: 'PurchaseInvoice',
        voucherNumber: 'PINV-001',
        hasLdc: true,
        certificateNumber: 'LDC-99',
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Party']).toBe('NonResident Tech');
    expect(csv[0]['Withheld Amount']).toBe('100.00');
    expect(csv[0]['LDC']).toBe('Yes');
  });

  it('calls getDetails with filters on generate', () => {
    const svcMock = {
      getDetails: vi.fn().mockReturnValue(of({ totalCount: 1, totalWithheldAmount: 100, rows: [] })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: TaxWithholdingReportService, useValue: svcMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new TaxWithholdingDetailsComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ partyType: 'Supplier', taxCategory: ' Section 107A ' });
    comp.generate();

    expect(svcMock.getDetails).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      partyType: 'Supplier',
      taxCategory: 'Section 107A',
    }));
    expect(comp.report()?.totalWithheldAmount).toBe(100);
  });
});
