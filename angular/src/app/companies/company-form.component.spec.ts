import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CompanyFormComponent } from './company-form.component';
import { CompanyService } from '../proxy/core/company.service';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';

describe('CompanyFormComponent LHDN TIN Lookup', () => {
  function createComponent(id: string | null = null) {
    const companyServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'comp-1', name: 'ACME Sdn Bhd', registrationNumber: '202001012345', isGroup: false, parentCompanyId: null })),
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'grp-1', name: 'ACME Holdings', currencyCode: 'MYR', isGroup: true },
          { id: 'comp-1', name: 'ACME Sdn Bhd', currencyCode: 'MYR', isGroup: false },
        ],
      })),
      create: vi.fn(),
      update: vi.fn().mockReturnValue(of({})),
      setupNewCompany: vi.fn(),
    };
    const einvoiceServiceMock = {
      searchTaxpayer: vi.fn().mockReturnValue(of({
        isSuccess: true,
        tin: 'C1234567890',
        name: 'ACME SDN BHD',
        isPersisted: false,
      })),
    };
    const toasterMock = { success: vi.fn(), warn: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: CompanyService, useValue: companyServiceMock },
        { provide: EInvoiceService, useValue: einvoiceServiceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => id } } } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CompanyFormComponent());
    return { comp, companyServiceMock, einvoiceServiceMock, toasterMock };
  }

  it('calls searchTaxpayer with companyId and BRN and updates taxId form control', () => {
    const { comp, einvoiceServiceMock, toasterMock } = createComponent('comp-1');
    comp.isEditMode = true;
    comp.entityId = 'comp-1';
    comp.form.patchValue({ name: 'ACME Sdn Bhd', registrationNumber: '202001012345' });

    comp.verifyTaxpayer();

    expect(einvoiceServiceMock.searchTaxpayer).toHaveBeenCalledWith({
      idType: 'BRN',
      idValue: '202001012345',
      taxpayerName: 'ACME Sdn Bhd',
      companyId: 'comp-1',
    });
    expect(comp.form.get('taxId')?.value).toBe('C1234567890');
    expect(toasterMock.success).toHaveBeenCalled();
  });

  it('loads group companies on init and saves parentCompanyId and isGroup', () => {
    const { comp, companyServiceMock } = createComponent('comp-1');
    comp.ngOnInit();

    expect(comp.groupCompanies().map(c => c.id)).toEqual(['grp-1']);

    comp.form.patchValue({ parentCompanyId: 'grp-1', isGroup: false });
    comp.save();

    expect(companyServiceMock.update).toHaveBeenCalledWith(
      'comp-1',
      expect.objectContaining({ parentCompanyId: 'grp-1', isGroup: false }),
    );
  });
});
