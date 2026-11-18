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
      get: vi.fn().mockReturnValue(of({ id: 'comp-1', name: 'ACME Sdn Bhd', registrationNumber: '202001012345' })),
      create: vi.fn(),
      update: vi.fn(),
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
    return { comp, einvoiceServiceMock, toasterMock };
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
});
