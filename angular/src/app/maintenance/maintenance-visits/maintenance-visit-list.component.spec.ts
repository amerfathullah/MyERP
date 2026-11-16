import { Injector, runInInjectionContext } from '@angular/core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { MaintenanceVisitListComponent } from './maintenance-visit-list.component';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { CustomerService } from '../../proxy/sales/customer.service';

describe('MaintenanceVisitListComponent Delete Visit', () => {
  it('deletes open maintenance visit after confirmation via MaintenanceService.deleteVisit', () => {
    const maintMock = {
      getVisitList: vi.fn().mockReturnValue(of({ items: [{ id: 'mv-1', completionStatus: 0 }], totalCount: 1 })),
      deleteVisit: vi.fn().mockReturnValue(of(void 0)),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: MaintenanceService, useValue: maintMock },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new MaintenanceVisitListComponent());
    comp.ngOnInit();
    comp.deleteVisit('mv-1');

    expect(maintMock.deleteVisit).toHaveBeenCalledWith('mv-1');
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyDeleted');
    expect(maintMock.getVisitList).toHaveBeenCalledTimes(2);
  });
});
