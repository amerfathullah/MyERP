import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { LeadDetailsComponent, buildLeadDetailsCsvRows } from './lead-details.component';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { LeadStatus } from '../../../proxy/crm/lead-status.enum';

describe('LeadDetailsComponent', () => {
  it('formats lead details rows for CSV export', () => {
    const csv = buildLeadDetailsCsvRows([
      {
        leadNumber: 'LD-001',
        leadName: 'Jane Doe',
        jobTitle: 'CTO',
        status: 'Qualified',
        source: 'Website',
        email: 'jane@example.com',
        country: 'Malaysia',
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Lead #']).toBe('LD-001');
    expect(csv[0]['Status']).toBe('Qualified');
  });

  it('calls getLeadDetails with selected status and country', () => {
    const svcMock = {
      getLeadDetails: vi.fn().mockReturnValue(of({ totalCount: 1, rows: [{ leadId: 'l1', leadNumber: 'LD-001' }] })),
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

    const comp = runInInjectionContext(injector, () => new LeadDetailsComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ status: LeadStatus.Qualified, country: ' Malaysia ' });
    comp.generate();

    expect(svcMock.getLeadDetails).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      status: LeadStatus.Qualified,
      country: 'Malaysia',
    }));
    expect(comp.report()?.totalCount).toBe(1);
  });
});
