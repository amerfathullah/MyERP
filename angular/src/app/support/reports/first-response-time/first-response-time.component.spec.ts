import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { FirstResponseTimeComponent, buildFirstResponseTimeCsvRows } from './first-response-time.component';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('FirstResponseTimeComponent', () => {
  it('formats first response time rows for CSV export', () => {
    const csv = buildFirstResponseTimeCsvRows([
      { date: '2026-11-18', issueCount: 4, avgFirstResponseTimeHours: 2.345 },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Issue Count']).toBe(4);
    expect(csv[0]['Avg First Response (h)']).toBe('2.35');
  });

  it('fetches first response time report on init when company context exists', () => {
    const svcMock = {
      getFirstResponseTime: vi.fn().mockReturnValue(of({
        totalIssues: 2,
        overallAverageHours: 1.75,
        rows: [{ date: '2026-11-18', issueCount: 2, avgFirstResponseTimeHours: 1.75 }],
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

    const comp = runInInjectionContext(injector, () => new FirstResponseTimeComponent());
    comp.ngOnInit();

    expect(svcMock.getFirstResponseTime).toHaveBeenCalledWith(expect.objectContaining({ companyId: 'c1' }));
    expect(comp.report()?.overallAverageHours).toBe(1.75);
  });
});
