import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { AgingReportComponent, buildPartySummaryCsvRows } from './aging-report.component';
import { AgingReportService } from '../../../proxy/accounting/aging-report.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CustomerGroupService } from '../../../proxy/core/customer-group.service';
import { SupplierGroupService } from '../../../proxy/core/supplier-group.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { AgingPartySummaryDto } from '../../../proxy/accounting/models';

describe('AgingReportComponent', () => {
  it('formats party summary rows with dynamic aging buckets for CSV export', () => {
    const summaries: AgingPartySummaryDto[] = [
      {
        partyId: 'c1',
        partyName: 'Acme Corp',
        partyGroupName: 'Enterprise',
        invoicedAmount: 1000,
        paidAmount: 400,
        creditNoteAmount: 50,
        advanceAmount: 0,
        outstandingAmount: 550,
        notYetDueAmount: 150,
        bucketTotals: [200, 100, 100, 0],
        totalDue: 400,
        invoiceCount: 3,
      },
    ];

    const rows = buildPartySummaryCsvRows(summaries, ['0-30', '31-60', '61-90', '91+']);
    expect(rows).toHaveLength(1);
    expect(rows[0]['Party']).toBe('Acme Corp');
    expect(rows[0]['Not Yet Due']).toBe('150.00');
    expect(rows[0]['0-30 Days']).toBe('200.00');
    expect(rows[0]['Total Due']).toBe('400.00');
    expect(rows[0]['Invoices']).toBe(3);
  });

  it('forwards partyGroupId, ageingBasedOn, and calculateAgeingWith and sums party summary columns', () => {
    const agingMock = {
      getReceivablesAging: vi.fn().mockReturnValue(of({
        reportType: 'Receivables',
        bucketLabels: ['0-30', '31-60', '61-90', '91+'],
        bucketTotals: [200, 100, 0, 0],
        totalOutstanding: 300,
        partySummaries: [
          { partyId: 'c1', invoicedAmount: 500, paidAmount: 200, outstandingAmount: 300, totalDue: 300, invoiceCount: 2 },
          { partyId: 'c2', invoicedAmount: 100, paidAmount: 100, outstandingAmount: 0, totalDue: 0, invoiceCount: 1 },
        ],
      })),
      getPayablesAging: vi.fn().mockReturnValue(of({})),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: AgingReportService, useValue: agingMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Main' }] })) } },
        { provide: CustomerGroupService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'cg-1', name: 'Enterprise' }] })) } },
        { provide: SupplierGroupService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sg-1', name: 'Vendors' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: (k: string) => (k === 'view' ? 'summary' : null) } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ToasterService, useValue: { info: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new AgingReportComponent());
    comp.ngOnInit();
    expect(comp.viewMode).toBe('summary');

    comp.filters.patchValue({
      partyGroupId: 'cg-1',
      ageingBasedOn: 'Posting Date',
      calculateAgeingWith: 'Today Date',
    });
    comp.generate();

    expect(agingMock.getReceivablesAging).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      partyGroupId: 'cg-1',
      ageingBasedOn: 'Posting Date',
      calculateAgeingWith: 'Today Date',
    }));
    expect(comp.sumSummaryField('invoicedAmount')).toBe(600);
    expect(comp.sumSummaryField('invoiceCount')).toBe(3);
  });
});
