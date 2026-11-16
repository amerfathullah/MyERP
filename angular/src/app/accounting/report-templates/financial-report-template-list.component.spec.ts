import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { FinancialReportTemplateListComponent } from './financial-report-template-list.component';
import { FinancialReportTemplateService } from '../../proxy/accounting/financial-report-template.service';

describe('FinancialReportTemplateListComponent', () => {
  let serviceMock: any;
  let toasterMock: any;
  let confirmationMock: any;
  let routerMock: any;

  function createComponent(): FinancialReportTemplateListComponent {
    const injector = Injector.create({
      providers: [
        { provide: FinancialReportTemplateService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: Router, useValue: routerMock },
      ],
    });
    return runInInjectionContext(injector, () => new FinancialReportTemplateListComponent());
  }

  beforeEach(() => {
    serviceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 't1', name: 'Balance Sheet', isEnabled: true, rows: [] }], totalCount: 1 })),
      create: vi.fn().mockReturnValue(of({})),
      toggle: vi.fn().mockReturnValue(of({})),
      delete: vi.fn().mockReturnValue(of(void 0)),
      validate: vi.fn().mockReturnValue(of([])),
    };
    toasterMock = { success: vi.fn(), warn: vi.fn(), error: vi.fn() };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
    routerMock = { navigate: vi.fn() };
  });

  it('loads templates on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(serviceMock.getList).toHaveBeenCalledTimes(1);
    expect(comp.templates()).toHaveLength(1);
  });

  it('shows success toast and clears errors when validate returns empty list', () => {
    const comp = createComponent();
    comp.validationErrors.set(['old error']);
    comp.validationTemplateName.set('Old');

    comp.validateTemplate({ id: 't1', name: 'Balance Sheet' } as any);

    expect(serviceMock.validate).toHaveBeenCalledWith('t1');
    expect(comp.validatingId()).toBeNull();
    expect(comp.validationErrors()).toBeNull();
    expect(comp.validationTemplateName()).toBe('');
    expect(toasterMock.success).toHaveBeenCalledWith('::TemplateValid');
  });

  it('displays validation errors when validate returns issues', () => {
    serviceMock.validate.mockReturnValue(of(['Row 3: Unknown code REV', 'Row 5: Circular reference']));
    const comp = createComponent();

    comp.validateTemplate({ id: 't2', name: 'Custom P&L' } as any);

    expect(comp.validatingId()).toBeNull();
    expect(comp.validationTemplateName()).toBe('Custom P&L');
    expect(comp.validationErrors()).toEqual(['Row 3: Unknown code REV', 'Row 5: Circular reference']);
    expect(toasterMock.warn).toHaveBeenCalledWith('::ValidationErrors');
  });

  it('resets validatingId and notifies on validate error', () => {
    serviceMock.validate.mockReturnValue(throwError(() => new Error('boom')));
    const comp = createComponent();

    comp.validateTemplate({ id: 't3', name: 'Broken' } as any);

    expect(comp.validatingId()).toBeNull();
    expect(toasterMock.error).toHaveBeenCalledWith('::OperationFailed');
  });
});
