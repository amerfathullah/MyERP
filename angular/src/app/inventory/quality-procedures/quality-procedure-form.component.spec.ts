import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { QualityProcedureFormComponent } from './quality-procedure-form.component';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';

describe('QualityProcedureFormComponent', () => {
  it('allows selecting a non-group parent procedure and saves via createProcedure', () => {
    const qmMock = {
      getProcedureList: vi.fn().mockReturnValue(of({
        items: [{ id: 'proc-1', name: 'Receiving Check', isGroup: false }],
      })),
      getProcedure: vi.fn(),
      createProcedure: vi.fn().mockReturnValue(of({ id: 'proc-2' })),
      updateProcedure: vi.fn(),
    };
    const routerMock = { navigate: vi.fn() };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: QualityManagementService, useValue: qmMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: Router, useValue: routerMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QualityProcedureFormComponent());
    comp.ngOnInit();

    expect(comp.parentProcedures).toHaveLength(1);
    comp.form.patchValue({
      name: 'Packaging Check',
      parentQualityProcedureId: 'proc-1',
      isGroup: false,
    });
    comp.save();

    expect(qmMock.createProcedure).toHaveBeenCalledWith(expect.objectContaining({
      name: 'Packaging Check',
      parentQualityProcedureId: 'proc-1',
      isGroup: false,
    }));
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullySaved');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/inventory/quality-procedures']);
  });
});
