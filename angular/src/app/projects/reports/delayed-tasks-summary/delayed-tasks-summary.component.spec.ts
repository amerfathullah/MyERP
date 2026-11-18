import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DelayedTasksSummaryComponent } from './delayed-tasks-summary.component';
import { ProjectService } from '../../../proxy/projects/project.service';
import { ProjectPriority } from '../../../proxy/projects/project-priority.enum';
import { ProjectTaskStatus } from '../../../proxy/projects/project-task-status.enum';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('DelayedTasksSummaryComponent', () => {
  let projectMock: { getDelayedTasksSummary: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: DelayedTasksSummaryComponent;

  beforeEach(() => {
    projectMock = {
      getDelayedTasksSummary: vi.fn().mockReturnValue(of({
        totalTasks: 2,
        delayedCount: 1,
        onTrackCount: 1,
        rows: [
          {
            taskId: 't-1',
            taskNumber: 'TASK-001',
            subject: 'Migrate GL reports',
            projectId: 'p-1',
            projectNumber: 'PROJ-001',
            projectName: 'ERP Upgrade',
            status: ProjectTaskStatus.Overdue,
            priority: ProjectPriority.High,
            progress: 50,
            delayInDays: 7,
          },
        ],
      })),
    };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: routerMock },
        { provide: ProjectService, useValue: projectMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new DelayedTasksSummaryComponent());
  });

  it('loads delayed tasks summary and exposes enum labels', () => {
    comp.ngOnInit();

    expect(projectMock.getDelayedTasksSummary).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
    }));
    expect(comp.report()?.delayedCount).toBe(1);
    expect(comp.statusLabel(ProjectTaskStatus.Overdue)).toBe('Overdue');
    expect(comp.priorityLabel(ProjectPriority.High)).toBe('High');
  });

  it('navigates to project detail on openProject', () => {
    comp.openProject({ projectId: 'p-1' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects', 'p-1']);
  });
});
