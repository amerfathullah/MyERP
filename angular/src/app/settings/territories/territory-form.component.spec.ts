import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { TerritoryFormComponent } from './territory-form.component';
import { TerritoryService } from '../../proxy/core/territory.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { FiscalYearService } from '../../proxy/accounting/fiscal-year.service';
import { ItemGroupService } from '../../proxy/inventory/item-group.service';

describe('TerritoryFormComponent', () => {
  it('loads targets on edit and calls addTarget / removeTarget on TerritoryService', () => {
    const initialTargets = [{ id: 'tgt-1', fiscalYearName: 'FY2026', targetQty: 10, targetAmount: 5000 }];
    const territoryMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
      get: vi.fn().mockReturnValue(of({ id: 'ter-1', name: 'Selangor', targets: initialTargets })),
      addTarget: vi.fn().mockReturnValue(of(void 0)),
      removeTarget: vi.fn().mockReturnValue(of(void 0)),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: TerritoryService, useValue: territoryMock },
        { provide: EmployeeService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: FiscalYearService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ItemGroupService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ter-1' } } } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new TerritoryFormComponent());
    comp.ngOnInit();

    expect(comp.targets()).toEqual(initialTargets);

    comp.newTarget = { fiscalYearId: 'fy-1', itemGroupId: 'ig-1', targetQty: 25, targetAmount: 10000 };
    comp.addTarget();
    expect(territoryMock.addTarget).toHaveBeenCalledWith('ter-1', {
      fiscalYearId: 'fy-1',
      itemGroupId: 'ig-1',
      targetQty: 25,
      targetAmount: 10000,
    });
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullySaved');

    comp.removeTarget('tgt-1');
    expect(territoryMock.removeTarget).toHaveBeenCalledWith('ter-1', 'tgt-1');
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyDeleted');
  });
});
