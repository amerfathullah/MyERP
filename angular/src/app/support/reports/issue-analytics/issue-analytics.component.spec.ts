import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { IssueAnalyticsComponent, buildIssueAnalyticsCsvRows } from './issue-analytics.component';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('IssueAnalyticsComponent', () => {
  it('formats issue analytics matrix rows for CSV export', () => {
    const csv = buildIssueAnalyticsCsvRows(
      [{ dimensionLabel: 'Bug', periodCounts: [3, 5], total: 8 }],
      ['Oct', 'Nov'],
    );
    expect(csv).toHaveLength(1);
    expect(csv[0]['Dimension']).toBe('Bug');
    expect(csv[0]['Oct']).toBe(3);
    expect(csv[0]['Nov']).toBe(5);
    expect(csv[0]['Total']).toBe(8);
  });

  it('calls getIssueAnalytics with selected range and basedOn', () => {
    const svcMock = {
      getIssueAnalytics: vi.fn().mockReturnValue(of({
        basedOn: 'Issue Type',
        range: 'Quarterly',
        periodLabels: ['Quarter 4'],
        rows: [{ dimensionKey: 'Bug', dimensionLabel: 'Bug', periodCounts: [4], total: 4 }],
        periodTotals: [4],
        grandTotal: 4,
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

    const comp = runInInjectionContext(injector, () => new IssueAnalyticsComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ basedOn: 'Issue Type', range: 'Quarterly' });
    comp.generate();

    expect(svcMock.getIssueAnalytics).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      basedOn: 'Issue Type',
      range: 'Quarterly',
    }));
    expect(comp.report()?.grandTotal).toBe(4);
  });
});
