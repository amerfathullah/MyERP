import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { SupportHourDistributionComponent, buildSupportHourCsvRows } from './support-hour-distribution.component';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('SupportHourDistributionComponent', () => {
  it('formats 3-hour slot distribution rows for CSV export', () => {
    const csv = buildSupportHourCsvRows(
      [{ date: '2026-11-18', slotCounts: [1, 0, 3], total: 4 }],
      ['12AM - 3AM', '3AM - 6AM', '6AM - 9AM'],
    );
    expect(csv).toHaveLength(1);
    expect(csv[0]['6AM - 9AM']).toBe(3);
    expect(csv[0]['Total']).toBe(4);
  });

  it('calls getSupportHourDistribution on init with company context', () => {
    const svcMock = {
      getSupportHourDistribution: vi.fn().mockReturnValue(of({
        timeSlotLabels: ['12AM - 3AM', '3AM - 6AM'],
        rows: [{ date: '2026-11-18', slotCounts: [2, 1], total: 3 }],
        slotTotals: [2, 1],
        grandTotal: 3,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: SupportReportService, useValue: svcMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SupportHourDistributionComponent());
    comp.ngOnInit();

    expect(svcMock.getSupportHourDistribution).toHaveBeenCalledWith(expect.objectContaining({ companyId: 'c1' }));
    expect(comp.report()?.grandTotal).toBe(3);
  });
});
