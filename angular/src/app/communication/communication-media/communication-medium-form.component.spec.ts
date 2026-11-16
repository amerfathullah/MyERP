import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CommunicationMediumFormComponent } from './communication-medium-form.component';
import { CommunicationMediumService } from '../../proxy/communication/communication-medium.service';
import { EmployeeGroupService } from '../../proxy/human-resources/employee-group.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

describe('CommunicationMediumFormComponent', () => {
  it('loads employee groups and suppliers and resolves handling group on routing check', () => {
    const mediumMock = {
      get: vi.fn().mockReturnValue(of({
        id: 'cm-1',
        communicationMediumType: 0,
        communicationChannel: '+603-5551234',
        catchAllEmployeeGroupId: 'eg-1',
        providerSupplierId: 'sup-1',
        isDisabled: false,
        timeslots: [{ dayOfWeek: 1, fromTime: '09:00', toTime: '17:00', employeeGroupId: 'eg-2' }],
      })),
      getHandlingEmployeeGroup: vi.fn().mockReturnValue(of('"eg-2"')),
      create: vi.fn(),
      update: vi.fn().mockReturnValue(of({})),
    };
    const egMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'eg-1', groupName: 'Fallback Support' },
          { id: 'eg-2', groupName: 'Tier-1 Voice' },
        ],
      })),
    };
    const supplierMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sup-1', name: 'Twilio' }] })),
    };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: CommunicationMediumService, useValue: mediumMock },
        { provide: EmployeeGroupService, useValue: egMock },
        { provide: SupplierService, useValue: supplierMock },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: (k: string) => k === 'id' ? 'cm-1' : null } } } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CommunicationMediumFormComponent());
    comp.ngOnInit();

    expect(comp.employeeGroups()).toHaveLength(2);
    expect(comp.suppliers()).toHaveLength(1);
    expect(comp.timeslotsArray.length).toBe(1);

    comp.testDay.set(1);
    comp.testTime.set('11:00');
    comp.checkHandlingGroup();

    expect(mediumMock.getHandlingEmployeeGroup).toHaveBeenCalledWith('cm-1', 1, '11:00');
    expect(comp.resolvedGroupLabel()).toBe('Tier-1 Voice');
  });
});
