import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { TimesheetBillingSummaryComponent } from './timesheet-billing-summary.component';
import { TimesheetService } from '../../../proxy/projects/timesheet.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('TimesheetBillingSummaryComponent', () => {
  let tsMock: { getTimesheetBillingSummary: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: TimesheetBillingSummaryComponent;

  beforeEach(() => {
    tsMock = {
      getTimesheetBillingSummary: vi.fn().mockReturnValue(of({
        currencyCode: 'MYR',
        totalWorkingHours: 10,
        totalBillingHours: 8,
        totalBillingAmount: 1600,
        rows: [
          {
            date: '2026-11-18T00:00:00Z',
            timesheetId: 'ts-1',
            timesheetLabel: 'TS-001',
            employeeName: 'Ali',
            projectId: 'p-1',
            projectName: 'ERP Rollout',
            workingHours: 10,
            billingHours: 8,
            billingAmount: 1600,
          },
        ],
      })),
    };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: routerMock },
        { provide: TimesheetService, useValue: tsMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new TimesheetBillingSummaryComponent());
  });

  it('loads timesheet billing summary with includeDraftTimesheets toggle', () => {
    comp.ngOnInit();

    expect(tsMock.getTimesheetBillingSummary).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      includeDraftTimesheets: false,
    }));
    expect(comp.report()?.totalBillingHours).toBe(8);
    expect(comp.report()?.totalBillingAmount).toBe(1600);
  });

  it('navigates to timesheet and project on click', () => {
    const row = { timesheetId: 'ts-1', projectId: 'p-1' };
    comp.openTimesheet(row);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects/timesheets', 'ts-1']);

    comp.openProject(row, { stopPropagation: vi.fn() } as unknown as Event);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects', 'p-1']);
  });
});
