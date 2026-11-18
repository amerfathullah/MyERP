import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { LeadConversionTimeComponent, buildLeadConversionCsvRows } from './lead-conversion-time.component';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('LeadConversionTimeComponent', () => {
  it('formats lead conversion time rows for CSV export', () => {
    const csv = buildLeadConversionCsvRows([
      {
        leadNumber: 'LD-002',
        leadName: 'John',
        customerName: 'Acme Ltd',
        interactions: 4,
        durationInDays: 12.5,
        supportTickets: 1,
        convertedOn: '2026-11-18',
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Customer']).toBe('Acme Ltd');
    expect(csv[0]['Duration (Days)']).toBe('12.50');
  });

  it('fetches lead conversion time report on init', () => {
    const svcMock = {
      getLeadConversionTime: vi.fn().mockReturnValue(of({
        totalConverted: 1,
        avgDurationInDays: 10,
        rows: [{ leadId: 'l1', customerName: 'Acme Ltd', durationInDays: 10 }],
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

    const comp = runInInjectionContext(injector, () => new LeadConversionTimeComponent());
    comp.ngOnInit();

    expect(svcMock.getLeadConversionTime).toHaveBeenCalledWith(expect.objectContaining({ companyId: 'c1' }));
    expect(comp.report()?.avgDurationInDays).toBe(10);
  });
});
