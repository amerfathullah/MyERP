import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { IdentityUserLookupService } from '@abp/ng.identity/proxy';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { EmployeeFormComponent } from './employee-form.component';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyService } from '../../proxy/core/company.service';
import { EmployeeStore } from '../store/employee.store';

describe('EmployeeFormComponent', () => {
  it('patches statutory fields and formatted dates in edit mode and normalizes blank dates on save', () => {
    const empMock = {
      get: vi.fn().mockReturnValue(of({
        id: 'emp-1',
        companyId: 'comp-1',
        firstName: 'Amina',
        lastName: 'Zain',
        dateOfBirth: '1992-04-15T00:00:00Z',
        dateOfJoining: '2024-01-10T00:00:00Z',
        epfNumber: 'EPF-12345',
        socsoNumber: 'SOC-67890',
        taxNumber: 'PCB-999',
        reportsToEmployeeId: null,
        userId: null,
      })),
      update: vi.fn().mockReturnValue(of({})),
      create: vi.fn(),
    };
    const routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: Router, useValue: routerMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: (k: string) => k === 'id' ? 'emp-1' : null } } } },
        { provide: EmployeeStore, useValue: {} },
        { provide: EmployeeService, useValue: empMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: IdentityUserLookupService, useValue: { search: vi.fn().mockReturnValue(of({ items: [] })), findById: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new EmployeeFormComponent());
    comp.ngOnInit();

    expect(comp.isEditMode).toBe(true);
    expect(comp.form.get('dateOfBirth')?.value).toBe('1992-04-15');
    expect(comp.form.get('dateOfJoining')?.value).toBe('2024-01-10');
    expect(comp.form.get('epfNumber')?.value).toBe('EPF-12345');
    expect(comp.form.get('socsoNumber')?.value).toBe('SOC-67890');
    expect(comp.form.get('taxNumber')?.value).toBe('PCB-999');

    comp.form.patchValue({ dateOfBirth: '' });
    comp.save();

    expect(empMock.update).toHaveBeenCalledWith('emp-1', expect.objectContaining({
      epfNumber: 'EPF-12345',
      socsoNumber: 'SOC-67890',
      taxNumber: 'PCB-999',
      dateOfBirth: null,
      dateOfJoining: '2024-01-10',
    }));
    expect(routerMock.navigate).toHaveBeenCalledWith(['/hr/employees']);
  });
});
