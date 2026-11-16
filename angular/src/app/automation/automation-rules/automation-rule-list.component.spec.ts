import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AutomationRuleListComponent } from './automation-rule-list.component';
import { AutomationRuleStore } from '../store/automation-rule.store';
import { AutomationRuleService } from '../../proxy/automation/automation-rule.service';

describe('AutomationRuleListComponent', () => {
  let storeMock: any;
  let ruleServiceMock: any;
  let confirmationMock: any;

  function createComponent(): AutomationRuleListComponent {
    const injector = Injector.create({
      providers: [
        { provide: AutomationRuleStore, useValue: storeMock },
        { provide: AutomationRuleService, useValue: ruleServiceMock },
        { provide: ConfirmationService, useValue: confirmationMock },
      ],
    });
    return runInInjectionContext(injector, () => new AutomationRuleListComponent());
  }

  beforeEach(() => {
    storeMock = {
      entities: signal([]),
      totalCount: signal(0),
      isLoading: signal(false),
      load: vi.fn(),
      toggleActive: vi.fn(),
      remove: vi.fn(),
    };
    ruleServiceMock = {
      getExecutionLogs: vi.fn().mockReturnValue(of({
        totalCount: 2,
        items: [
          { id: 'l1', automationRuleId: 'r1', sourceDocumentType: 'SalesInvoice', sourceDocumentId: 'SI-001', isSuccess: true, executionDurationMs: 42 },
          { id: 'l2', automationRuleId: 'r1', sourceDocumentType: 'SalesInvoice', sourceDocumentId: 'SI-002', isSuccess: false, errorMessage: 'Missing TIN', executionDurationMs: 18 },
        ],
      })),
    };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
  });

  it('loads rules on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(storeMock.load).toHaveBeenCalledWith({ skipCount: 0, maxResultCount: 10, sorting: '' });
  });

  it('loads execution logs when clicking View Logs on a rule', () => {
    const comp = createComponent();
    const rule = { id: 'r1', name: 'Auto Submit e-Invoice' } as any;

    comp.viewExecutionLogs(rule);

    expect(comp.selectedRule()).toEqual(rule);
    expect(ruleServiceMock.getExecutionLogs).toHaveBeenCalledWith('r1', { skipCount: 0, maxResultCount: 20, sorting: '' });
    expect(comp.executionLogs()).toHaveLength(2);
    expect(comp.isLoadingLogs()).toBe(false);
  });

  it('closes execution logs panel when clicking the already-selected rule', () => {
    const comp = createComponent();
    const rule = { id: 'r1', name: 'Auto Submit e-Invoice' } as any;

    comp.viewExecutionLogs(rule);
    expect(comp.selectedRule()).toEqual(rule);

    comp.viewExecutionLogs(rule);
    expect(comp.selectedRule()).toBeNull();
    expect(comp.executionLogs()).toEqual([]);
  });

  it('clears logs and loading flag when getExecutionLogs fails', () => {
    ruleServiceMock.getExecutionLogs.mockReturnValue(throwError(() => new Error('network')));
    const comp = createComponent();

    comp.viewExecutionLogs({ id: 'r2', name: 'Broken Rule' } as any);

    expect(comp.executionLogs()).toEqual([]);
    expect(comp.isLoadingLogs()).toBe(false);
  });
});
