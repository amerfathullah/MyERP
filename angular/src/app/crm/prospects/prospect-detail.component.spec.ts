import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ProspectDetailComponent } from './prospect-detail.component';
import { ProspectService } from '../../proxy/crm/prospect.service';
import { LeadService } from '../../proxy/crm/lead.service';
import { CustomerService } from '../../proxy/sales/customer.service';

describe('ProspectDetailComponent Lead Linking', () => {
  function createComponent() {
    const updatedProspect = {
      id: 'pros-1',
      prospectName: 'MegaGroup',
      leadCount: 1,
      leads: [{ id: 'pl-1', leadId: 'lead-1', leadName: 'Jane Doe', email: 'jane@mega.my' }],
    };
    const prospectServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'pros-1', prospectName: 'MegaGroup', leadCount: 0, leads: [] })),
      addLead: vi.fn().mockReturnValue(of(updatedProspect)),
      convertToCustomer: vi.fn(),
    };
    const leadServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'lead-1', fullName: 'Jane Doe', email: 'jane@mega.my' }] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'pros-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ProspectService, useValue: prospectServiceMock },
        { provide: LeadService, useValue: leadServiceMock },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ProspectDetailComponent());
    comp.ngOnInit();
    return { comp, prospectServiceMock, toasterMock };
  }

  it('loads available leads and links selected lead to prospect', () => {
    const { comp, prospectServiceMock, toasterMock } = createComponent();
    expect(comp.leads()).toHaveLength(1);

    comp.selectedLeadId.set('lead-1');
    comp.linkSelectedLead();

    expect(prospectServiceMock.addLead).toHaveBeenCalledWith('pros-1', 'lead-1', 'Jane Doe', 'jane@mega.my');
    expect(comp.prospect()?.leadCount).toBe(1);
    expect(comp.selectedLeadId()).toBe('');
    expect(toasterMock.success).toHaveBeenCalledWith('::LeadLinked');
  });
});
