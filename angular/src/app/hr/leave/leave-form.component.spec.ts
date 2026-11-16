import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { LeaveFormComponent } from './leave-form.component';
import { LeaveService } from '../../proxy/human-resources/leave.service';
import { LeaveAllocationService } from '../../proxy/human-resources/leave-allocation.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('LeaveFormComponent Allocation Balance', () => {
  function createComponent() {
    const leaveAllocMock = {
      getBalance: vi.fn().mockReturnValue(of(12.5)),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: LeaveService, useValue: { getLeaveTypes: vi.fn().mockReturnValue(of([])), apply: vi.fn() } },
        { provide: LeaveAllocationService, useValue: leaveAllocMock },
        { provide: EmployeeService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new LeaveFormComponent());
    comp.ngOnInit();
    return { comp, leaveAllocMock };
  }

  it('fetches live leave balance when employee and leave type are selected', () => {
    const { comp, leaveAllocMock } = createComponent();
    expect(comp.leaveBalance()).toBeNull();

    comp.form.patchValue({
      employeeId: 'emp-1',
      leaveTypeId: 'lt-annual',
      fromDate: '2026-12-01',
    });

    expect(leaveAllocMock.getBalance).toHaveBeenCalledWith('emp-1', 'lt-annual', '2026-12-01');
    expect(comp.leaveBalance()).toBe(12.5);
  });
});
