import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  SalesPipelineAnalyticsComponent,
  buildPipelineAnalyticsCsvRows,
} from './sales-pipeline-analytics.component';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('SalesPipelineAnalyticsComponent', () => {
  it('formats pipeline analytics rows for CSV export', () => {
    const csv = buildPipelineAnalyticsCsvRows(
      [{ groupLabel: 'Proposal', periodValues: [1000, 2500], total: 3500 }],
      ['Oct', 'Nov'],
    );
    expect(csv).toHaveLength(1);
    expect(csv[0]['Group']).toBe('Proposal');
    expect(csv[0]['Nov']).toBe(2500);
    expect(csv[0]['Total']).toBe(3500);
  });

  it('calls getSalesPipelineAnalytics with pipelineBy, basedOn, and range', () => {
    const svcMock = {
      getSalesPipelineAnalytics: vi.fn().mockReturnValue(of({
        pipelineBy: 'Owner',
        basedOn: 'Number',
        range: 'Quarterly',
        periodLabels: ['Quarter 4'],
        rows: [{ groupKey: 'u1', groupLabel: 'Alice', periodValues: [5], total: 5 }],
        periodTotals: [5],
        grandTotal: 5,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: CrmReportService, useValue: svcMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SalesPipelineAnalyticsComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ pipelineBy: 'Owner', basedOn: 'Number', range: 'Quarterly' });
    comp.generate();

    expect(svcMock.getSalesPipelineAnalytics).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      pipelineBy: 'Owner',
      basedOn: 'Number',
      range: 'Quarterly',
    }));
    expect(comp.report()?.grandTotal).toBe(5);
  });
});
