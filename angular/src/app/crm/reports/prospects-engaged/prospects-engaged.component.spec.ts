import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { ProspectsEngagedComponent, buildProspectsEngagedCsvRows } from './prospects-engaged.component';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ProspectsEngagedComponent', () => {
  it('formats engaged prospects rows for CSV export', () => {
    const csv = buildProspectsEngagedCsvRows([
      {
        leadNumber: 'LD-010',
        leadName: 'Sam Lee',
        organizationName: 'Beta Tech',
        referenceType: 'Opportunity',
        referenceName: 'OPP-010',
        interactionCount: 3,
        lastCommunication: 'Follow up call',
        lastCommunicationDate: '2026-11-18',
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Reference Type']).toBe('Opportunity');
    expect(csv[0]['Interactions']).toBe(3);
  });

  it('calls getProspectsEngagedButNotConverted with leadAgeDays and minInteractions', () => {
    const svcMock = {
      getProspectsEngagedButNotConverted: vi.fn().mockReturnValue(of({ totalCount: 1, rows: [{ leadId: 'l1' }] })),
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

    const comp = runInInjectionContext(injector, () => new ProspectsEngagedComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ leadAgeDays: 90, minInteractions: 2 });
    comp.generate();

    expect(svcMock.getProspectsEngagedButNotConverted).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      leadAgeDays: 90,
      minInteractions: 2,
    }));
    expect(comp.report()?.totalCount).toBe(1);
  });
});
