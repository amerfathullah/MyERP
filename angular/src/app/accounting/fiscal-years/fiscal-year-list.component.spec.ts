import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { FiscalYearListComponent } from './fiscal-year-list.component';
import { FiscalYearService } from '../../proxy/accounting/fiscal-year.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('FiscalYearListComponent', () => {
  let fyServiceMock: any;
  let companyServiceMock: any;
  let companyContextMock: any;
  let toasterMock: any;
  let confirmationMock: any;

  function createComponent(): FiscalYearListComponent {
    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: FiscalYearService, useValue: fyServiceMock },
        { provide: CompanyService, useValue: companyServiceMock },
        { provide: CompanyContextService, useValue: companyContextMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: confirmationMock },
      ],
    });
    return runInInjectionContext(injector, () => new FiscalYearListComponent());
  }

  beforeEach(() => {
    fyServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'fy-2026', name: 'FY 2026' }] })),
      getCurrent: vi.fn().mockReturnValue(of({ id: 'fy-2026', name: 'FY 2026', companyId: 'comp-1' })),
      create: vi.fn().mockReturnValue(of({ id: 'fy-2027', name: 'FY 2027' })),
      close: vi.fn().mockReturnValue(of({})),
    };
    companyServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'MyERP Sdn Bhd' }, { id: 'comp-2', name: 'Subsidiary' }] })),
    };
    companyContextMock = { currentCompanyId: signal('comp-1') };
    toasterMock = { success: vi.fn(), error: vi.fn() };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
  });

  it('loads current fiscal year for active company context on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.selectedCompanyId()).toBe('comp-1');
    expect(fyServiceMock.getCurrent).toHaveBeenCalledWith('comp-1');
    expect(comp.currentFiscalYear()?.id).toBe('fy-2026');
  });

  it('falls back to first company when no company context is active', () => {
    companyContextMock.currentCompanyId.set('');
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.selectedCompanyId()).toBe('comp-1');
    expect(fyServiceMock.getCurrent).toHaveBeenCalledWith('comp-1');
  });

  it('reloads current fiscal year when switching selected company', () => {
    fyServiceMock.getCurrent.mockReturnValue(of({ id: 'fy-sub', name: 'FY 2026 Sub', companyId: 'comp-2' }));
    const comp = createComponent();

    comp.loadCurrentFiscalYear('comp-2');

    expect(comp.selectedCompanyId()).toBe('comp-2');
    expect(fyServiceMock.getCurrent).toHaveBeenCalledWith('comp-2');
    expect(comp.currentFiscalYear()?.name).toBe('FY 2026 Sub');
  });

  it('clears currentFiscalYear when getCurrent errors', () => {
    fyServiceMock.getCurrent.mockReturnValue(throwError(() => new Error('not found')));
    const comp = createComponent();

    comp.loadCurrentFiscalYear('comp-1');

    expect(comp.currentFiscalYear()).toBeNull();
  });
});
