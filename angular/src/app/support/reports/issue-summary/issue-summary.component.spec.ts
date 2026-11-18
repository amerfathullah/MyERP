import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { IssueSummaryComponent, buildIssueSummaryCsvRows } from './issue-summary.component';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { IssueSummaryRowDto } from '../../../proxy/support/models';

describe('IssueSummaryComponent', () => {
  it('formats issue summary rows for CSV export', () => {
    const rows: IssueSummaryRowDto[] = [
      {
        dimensionLabel: 'High',
        openCount: 2,
        repliedCount: 1,
        onHoldCount: 0,
        closedCount: 5,
        cancelledCount: 0,
        totalIssues: 8,
        slaFulfilledCount: 4,
        slaFailedCount: 1,
        firstResponseDueCount: 1,
        resolutionDueCount: 1,
        avgFirstResponseTimeHours: 1.5,
        avgHoldTimeHours: 0.25,
        avgResolutionTimeHours: 6.75,
      },
    ];

    const csv = buildIssueSummaryCsvRows(rows);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Dimension']).toBe('High');
    expect(csv[0]['Total Issues']).toBe(8);
    expect(csv[0]['Avg First Response (h)']).toBe('1.50');
  });

  it('loads issue summary report for selected company and grouping', () => {
    const mockReport = {
      basedOn: 'Issue Priority',
      totalIssues: 3,
      rows: [{ dimensionKey: 'High', dimensionLabel: 'High', totalIssues: 3 }],
    };
    const svcMock = { getIssueSummary: vi.fn().mockReturnValue(of(mockReport)) };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: SupportReportService, useValue: svcMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new IssueSummaryComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ basedOn: 'Issue Priority' });
    comp.generate();

    expect(svcMock.getIssueSummary).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      basedOn: 'Issue Priority',
    }));
    expect(comp.report()?.totalIssues).toBe(3);
  });
});
