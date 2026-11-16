import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { QualityFeedbackFormComponent } from './quality-feedback-form.component';
import { QualityFeedbackListComponent } from './quality-feedback-list.component';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('QualityFeedback Template & Inspection', () => {
  it('loads template parameters via getFeedbackTemplate on template selection', () => {
    const qmsMock = {
      getFeedbackTemplateList: vi.fn().mockReturnValue(of({ items: [{ id: 'tpl-1', templateName: 'Service Survey' }] })),
      getFeedbackTemplate: vi.fn().mockReturnValue(of({
        id: 'tpl-1',
        templateName: 'Service Survey',
        parameters: [{ parameter: 'Speed' }, { parameter: 'Packaging' }],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: QualityManagementService, useValue: qmsMock },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QualityFeedbackFormComponent());
    comp.ngOnInit();
    comp.templateId = 'tpl-1';
    comp.onTemplateChange();

    expect(qmsMock.getFeedbackTemplate).toHaveBeenCalledWith('tpl-1');
    expect(comp.parameterRows.map(r => r.parameter)).toEqual(['Speed', 'Packaging']);
  });

  it('loads full feedback details via getFeedback on inspectFeedback', () => {
    const qmsMock = {
      getFeedbackList: vi.fn().mockReturnValue(of({ items: [{ id: 'fb-1', documentName: 'CUST-1' }], totalCount: 1 })),
      getFeedback: vi.fn().mockReturnValue(of({ id: 'fb-1', documentName: 'CUST-1', parameters: [{ parameter: 'Speed', rating: 5 }] })),
    };
    const injector = Injector.create({
      providers: [{ provide: QualityManagementService, useValue: qmsMock }],
    });

    const comp = runInInjectionContext(injector, () => new QualityFeedbackListComponent());
    comp.ngOnInit();
    comp.inspectFeedback('fb-1');

    expect(qmsMock.getFeedback).toHaveBeenCalledWith('fb-1');
    expect(comp.selectedFeedback?.parameters).toHaveLength(1);
  });
});
