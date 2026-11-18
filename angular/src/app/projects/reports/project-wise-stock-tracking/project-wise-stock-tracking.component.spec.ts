import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectWiseStockTrackingComponent } from './project-wise-stock-tracking.component';
import { ProjectService } from '../../../proxy/projects/project.service';
import { ProjectStatus } from '../../../proxy/projects/project-status.enum';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('ProjectWiseStockTrackingComponent', () => {
  let comp: ProjectWiseStockTrackingComponent;
  let projectMock: {
    getList: ReturnType<typeof vi.fn>;
    getProjectWiseStockTracking: ReturnType<typeof vi.fn>;
  };
  let toasterMock: { error: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };

  const mockReport = {
    currencyCode: 'MYR',
    totalPurchasedCost: 1200,
    totalIssuedCost: 450,
    totalDeliveredCost: 300,
    totalEstimatedCost: 5000,
    rows: [
      {
        projectId: 'p1',
        projectNumber: 'PROJ-001',
        projectName: 'Site Upgrade',
        status: ProjectStatus.Open,
        companyId: 'company-1',
        companyName: 'Acme Sdn Bhd',
        customerName: 'Apex Client',
        costOfPurchasedItems: 1200,
        costOfIssuedItems: 450,
        costOfDeliveredItems: 300,
        estimatedCost: 5000,
        expectedStartDate: '2026-01-10',
        expectedEndDate: '2026-06-30',
      },
    ],
  };

  beforeEach(() => {
    projectMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
      getProjectWiseStockTracking: vi.fn().mockReturnValue(of(mockReport)),
    };
    toasterMock = { error: vi.fn() };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: routerMock },
        { provide: ProjectService, useValue: projectMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'company-1' } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    comp = runInInjectionContext(injector, () => new ProjectWiseStockTrackingComponent());
    comp.ngOnInit();
  });

  it('loads project-wise stock tracking report on init', () => {
    expect(comp.report()?.totalPurchasedCost).toBe(1200);
    expect(comp.report()?.totalIssuedCost).toBe(450);
    expect(comp.report()?.totalDeliveredCost).toBe(300);
    expect(comp.loading()).toBe(false);
  });

  it('navigates to project detail on row click', () => {
    comp.openProject({ projectId: 'p1' });
    expect(routerMock.navigate).toHaveBeenCalledWith(['/projects', 'p1']);
  });

  it('shows toaster error when report request fails', () => {
    projectMock.getProjectWiseStockTracking.mockReturnValue(
      throwError(() => ({ error: { error: { message: 'Boom' } } }))
    );
    comp.loadReport();
    expect(toasterMock.error).toHaveBeenCalledWith('Boom');
    expect(comp.loading()).toBe(false);
  });
});
