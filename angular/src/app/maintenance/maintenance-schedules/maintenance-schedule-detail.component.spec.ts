import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { MaintenanceScheduleDetailComponent } from './maintenance-schedule-detail.component';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { MaintenanceScheduleService } from '../../proxy/maintenance/maintenance-schedule.service';
import { MaintenanceVisitService } from '../../proxy/maintenance/maintenance-visit.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CustomerService } from '../../proxy/sales/customer.service';

describe('MaintenanceScheduleDetailComponent', () => {
  it('generates and creates a MaintenanceVisit for a scheduled row and navigates to the new visit', () => {
    const draftVisit = {
      companyId: 'comp-1',
      customerId: 'cust-1',
      visitDate: '2026-11-20',
      maintenanceType: 0,
      maintenanceScheduleId: 'ms-1',
      maintenanceScheduleDetailId: 'msd-1',
      purposes: [],
    };
    const scheduleMock = {
      generateSchedule: vi.fn(),
      makeMaintenanceVisit: vi.fn().mockReturnValue(of(draftVisit)),
    };
    const visitMock = {
      create: vi.fn().mockReturnValue(of({ id: 'mv-99' })),
    };
    const routerMock = { navigate: vi.fn() };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ms-1' } } } },
        { provide: Router, useValue: routerMock },
        { provide: MaintenanceService, useValue: { getSchedule: vi.fn().mockReturnValue(of({ id: 'ms-1', status: 1, details: [] })) } },
        { provide: MaintenanceScheduleService, useValue: scheduleMock },
        { provide: MaintenanceVisitService, useValue: visitMock },
        { provide: ItemService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
        { provide: CustomerService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new MaintenanceScheduleDetailComponent());
    comp.makeVisit('msd-1');

    expect(scheduleMock.makeMaintenanceVisit).toHaveBeenCalledWith('ms-1', { scheduleDetailId: 'msd-1' });
    expect(visitMock.create).toHaveBeenCalledWith(draftVisit);
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyCreated');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/maintenance/visits', 'mv-99']);
  });
});
