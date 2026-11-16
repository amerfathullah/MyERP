import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { IncomingCallSettingsComponent } from './incoming-call-settings.component';
import { IncomingCallSettingsService } from '../../proxy/telephony/incoming-call-settings.service';
import { EmployeeGroupService } from '../../proxy/human-resources/employee-group.service';

describe('IncomingCallSettingsComponent', () => {
  it('loads employee groups and checks active group for day and time', () => {
    const settingsMock = {
      get: vi.fn().mockReturnValue(of({
        callRouting: 0,
        greetingMessage: 'Hello',
        schedules: [{ dayOfWeek: 2, fromTime: '09:00', toTime: '17:00', employeeGroupId: 'eg-1' }],
      })),
      getActiveEmployeeGroup: vi.fn().mockReturnValue(of('"eg-1"')),
      update: vi.fn().mockReturnValue(of({})),
    };
    const egMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'eg-1', groupName: 'Sales Desk' }] })),
    };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: IncomingCallSettingsService, useValue: settingsMock },
        { provide: EmployeeGroupService, useValue: egMock },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new IncomingCallSettingsComponent());
    comp.ngOnInit();

    expect(comp.employeeGroups()).toHaveLength(1);
    expect(comp.schedulesArray.length).toBe(1);

    comp.testDay.set(2);
    comp.testTime.set('10:30');
    comp.checkActiveGroup();

    expect(settingsMock.getActiveEmployeeGroup).toHaveBeenCalledWith(2, '10:30');
    expect(comp.activeGroupLabel()).toBe('Sales Desk');
  });
});
