import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectSummaryComponent, formatProjectStatusLabel } from './project-summary.component';
import { ProjectService } from '../../../proxy/projects/project.service';
import { ProjectStatus } from '../../../proxy/projects/project-status.enum';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ProjectSummaryComponent', () => {
  let projectMock: { getProjectSummaryReport: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: ProjectSummaryComponent;

  beforeEach(() => {
    projectMock = {
      getProjectSummaryReport: vi.fn().mockReturnValue(of({
        totalProjects: 2,
        openProjects: 1,
        completedProjects: 1,
        totalTasks: 5,
        completedTasks: 3,
        overdueTasks: 1,
        rows: [
          {
            projectId: 'p-1',
            projectNumber: 'PROJ-001',
            projectName: 'ERP Upgrade',
            status: ProjectStatus.Open,
            totalTasks: 5,
            completedTasks: 3,
            overdueTasks: 1,
            percentComplete: 60,
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

    comp = runInInjectionContext(injector, () => new ProjectSummaryComponent());
  });

  it('loads project summary report and formats status labels', () => {
    comp.ngOnInit();

    expect(projectMock.getProjectSummaryReport).toHaveBeenCalledWith({
      companyId: 'comp-1',
      status: null,
    });
    expect(comp.report()?.overdueTasks).toBe(1);
    expect(formatProjectStatusLabel(ProjectStatus.Completed)).toBe('Completed');
  });

  it('navigates to project detail on row click', () => {
    comp.openProject({ projectId: 'p-1' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects', 'p-1']);
  });
});
