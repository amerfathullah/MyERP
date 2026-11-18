import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  TdsComputationSummaryComponent,
  buildTdsComputationCsvRows,
} from './tds-computation-summary.component';
import { TaxWithholdingReportService } from '../../proxy/tax/tax-withholding-report.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('TdsComputationSummaryComponent', () => {
  it('formats TDS computation summary rows for CSV export', () => {
    const csv = buildTdsComputationCsvRows([
      {
        tin: 'C999',
        partyType: 'Supplier',
        partyName: 'Consulting GmbH',
        taxCategory: 'Section 107A',
        rate: 10,
        hasLdc: false,
        transactionCount: 2,
        totalTaxableAmount: 5000,
        totalWithheldAmount: 500,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['TIN']).toBe('C999');
    expect(csv[0]['Withheld Amount']).toBe('500.00');
  });

  it('calls getTdsComputationSummary on init', () => {
    const svcMock = {
      getTdsComputationSummary: vi.fn().mockReturnValue(of({
        fiscalYearName: 'FY2026',
        totalCount: 1,
        totalWithheldAmount: 500,
        rows: [],
      })),
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

    const comp = runInInjectionContext(injector, () => new TdsComputationSummaryComponent());
    comp.ngOnInit();

    expect(svcMock.getTdsComputationSummary).toHaveBeenCalledWith(expect.objectContaining({ companyId: 'c1' }));
    expect(comp.report()?.fiscalYearName).toBe('FY2026');
  });
});
