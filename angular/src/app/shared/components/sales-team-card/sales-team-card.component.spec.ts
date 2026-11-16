import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SalesTeamCardComponent } from './sales-team-card.component';
import { SalesPersonService } from '../../../proxy/sales/sales-person.service';

describe('SalesTeamCardComponent', () => {
  it('validates 100% split and emits salesTeamSaved payload', () => {
    const spMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sp-1', salesPersonName: 'Alice' }] })),
    };
    const toasterMock = { warn: vi.fn(), success: vi.fn(), error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: SalesPersonService, useValue: spMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SalesTeamCardComponent());
    const emitted: unknown[] = [];
    comp.salesTeamSaved.subscribe(v => emitted.push(v));

    comp.openEdit();
    expect(comp.editing()).toBe(true);
    expect(spMock.getList).toHaveBeenCalled();

    comp.draft.set([{ salesPersonId: 'sp-1', allocatedPercentage: 60, commissionRate: 5 }]);
    comp.save();
    expect(toasterMock.warn).toHaveBeenCalledWith('::SalesTeamMustEqual100');
    expect(emitted).toHaveLength(0);

    comp.draft.set([{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);
    comp.save();
    expect(emitted).toEqual([[{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]]);
  });
});
