import { Injector, runInInjectionContext } from '@angular/core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SalesPartnerListComponent } from './sales-partner-list.component';
import { SalesPartnerService } from '../../proxy/sales/sales-partner.service';

describe('SalesPartnerListComponent Published Filter', () => {
  it('switches to getPublishedPartners when published filter is toggled on', () => {
    const partnerMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sp-1', name: 'All Partner' }], totalCount: 1 })),
      getPublishedPartners: vi.fn().mockReturnValue(of({ items: [{ id: 'sp-2', name: 'Published Partner' }], totalCount: 1 })),
    };

    const injector = Injector.create({
      providers: [
        { provide: SalesPartnerService, useValue: partnerMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SalesPartnerListComponent());
    comp.ngOnInit();
    expect(comp.partners).toHaveLength(1);
    expect(comp.partners[0].name).toBe('All Partner');

    comp.togglePublishedFilter();
    expect(comp.publishedOnly).toBe(true);
    expect(partnerMock.getPublishedPartners).toHaveBeenCalledWith({ skipCount: 0, maxResultCount: 50, sorting: '' });
    expect(comp.partners).toHaveLength(1);
    expect(comp.partners[0].name).toBe('Published Partner');
  });
});
