import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ContractFormComponent } from './contract-form.component';
import { ContractService } from '../../proxy/crm/contract.service';
import { ContractTemplateService } from '../../proxy/crm/contract-template.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CustomerService } from '../../proxy/sales/customer.service';

describe('ContractFormComponent', () => {
  function createComponent(options?: { renderResult?: any; renderError?: boolean }) {
    const templateServiceMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [
          { id: 'tpl-1', title: 'Standard MSA', requiresFulfilment: true },
          { id: 'tpl-2', title: 'Simple NDA', requiresFulfilment: false },
        ],
      })),
      renderContractTerms: options?.renderError
        ? vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Render failed' } } })))
        : vi.fn().mockReturnValue(of(options?.renderResult ?? {
          templateId: 'tpl-1',
          contractTerms: 'Agreement with Acme Corp for RM 5000',
        })),
    };
    const customerServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'cust-1', name: 'Acme Corp' }] })),
    };
    const supplierServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ContractService, useValue: { get: vi.fn(), create: vi.fn(), update: vi.fn() } },
        { provide: ContractTemplateService, useValue: templateServiceMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: CompanyContextService, useValue: { currentCompanyId: 'comp-1' } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: SupplierService, useValue: supplierServiceMock },
        { provide: CustomerService, useValue: customerServiceMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ContractFormComponent());
    comp.ngOnInit();
    return { comp, templateServiceMock, toasterMock };
  }

  it('loads contract templates on init', () => {
    const { comp, templateServiceMock } = createComponent();
    expect(templateServiceMock.getList).toHaveBeenCalledWith({ maxResultCount: 100, skipCount: 0 });
    expect(comp.templates().length).toBe(2);
  });

  it('renders selected template with contract context and populates notes and fulfilment', () => {
    const { comp, templateServiceMock, toasterMock } = createComponent();
    comp.form.patchValue({
      contractNumber: 'CON-2026-001',
      partyType: 'Customer',
      partyId: 'cust-1',
      startDate: '2026-01-01',
      endDate: '2026-12-31',
      contractValue: 5000,
    });
    comp.selectedTemplateId.set('tpl-1');

    comp.applyTemplate();

    expect(templateServiceMock.renderContractTerms).toHaveBeenCalledWith({
      templateId: 'tpl-1',
      context: expect.objectContaining({
        contract_number: 'CON-2026-001',
        party_name: 'Acme Corp',
        partyName: 'Acme Corp',
        contract_value: '5000',
      }),
    });
    expect(comp.form.get('notes')?.value).toBe('Agreement with Acme Corp for RM 5000');
    expect(comp.form.get('requiresFulfilment')?.value).toBe(true);
    expect(toasterMock.success).toHaveBeenCalled();
  });

  it('handles template render error cleanly', () => {
    const { comp, toasterMock } = createComponent({ renderError: true });
    comp.selectedTemplateId.set('tpl-1');

    comp.applyTemplate();

    expect(comp.isRenderingTemplate()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Render failed');
  });
});
