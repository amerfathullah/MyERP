import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DailyTimesheetSummaryComponent } from './daily-timesheet-summary.component';
import { TimesheetService } from '../../../proxy/projects/timesheet.service';
import { TimesheetStatus } from '../../../proxy/projects/timesheet-status.enum';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('DailyTimesheetSummaryComponent', () => {
  let tsMock: { getDailyTimesheetSummary: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: DailyTimesheetSummaryComponent;

  beforeEach(() => {
    tsMock = {
      getDailyTimesheetSummary: vi.fn().mockReturnValue(of({
        totalHours: 7.5,
        entryCount: 2,
        rows: [
          {
            timesheetId: 'ts-1',
            timesheetLabel: 'TS-001',
            employeeName: 'Ali',
            fromTime: '2026-11-18T09:00:00Z',
            toTime: '2026-11-18T12:30:00Z',
            hours: 3.5,
            activityType: 'Development',
            projectId: 'p-1',
            projectName: 'ERP Rollout',
            status: TimesheetStatus.Submitted,
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

    comp = runInInjectionContext(injector, () => new DailyTimesheetSummaryComponent());
  });

  it('loads daily timesheet summary and formats datetimes cleanly', () => {
    comp.ngOnInit();

    expect(tsMock.getDailyTimesheetSummary).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
    }));
    expect(comp.report()?.totalHours).toBe(7.5);
    expect(comp.formatDateTime('2026-11-18T09:00:00Z')).toBe('2026-11-18 09:00');
    expect(comp.statusLabel(TimesheetStatus.Submitted)).toBe('Submitted');
  });

  it('navigates to timesheet and project on click', () => {
    const row = { timesheetId: 'ts-1', projectId: 'p-1' };
    comp.openTimesheet(row);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects/timesheets', 'ts-1']);

    comp.openProject(row, { stopPropagation: vi.fn() } as unknown as Event);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects', 'p-1']);
  });
});
