import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TaxCategoriesComponent } from './tax-categories.component';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { TaxType } from '../../proxy/tax/tax-type.enum';

describe('TaxCategoriesComponent', () => {
  let categoryServiceMock: any;
  let ruleServiceMock: any;
  let toasterMock: any;
  let confirmationMock: any;

  function createComponent(): TaxCategoriesComponent {
    const injector = Injector.create({
      providers: [
        { provide: TaxCategoryService, useValue: categoryServiceMock },
        { provide: TaxRuleService, useValue: ruleServiceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: FormBuilder, useValue: new FormBuilder() },
      ],
    });
    return runInInjectionContext(injector, () => new TaxCategoriesComponent());
  }

  beforeEach(() => {
    categoryServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      create: vi.fn().mockReturnValue(of({ id: 'cat-1', code: 'SST6', name: 'Service Tax 6%', taxType: TaxType.Service })),
      delete: vi.fn().mockReturnValue(of(void 0)),
      getDefaultTaxLines: vi.fn().mockReturnValue(of([
        { taxName: 'Sales Tax 10% (10%)', rate: 10, chargeType: 'OnNetTotal', taxCategoryCode: 'SST10' },
      ])),
    };
    ruleServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      create: vi.fn().mockReturnValue(of({})),
      update: vi.fn().mockReturnValue(of({})),
      delete: vi.fn().mockReturnValue(of(void 0)),
    };
    toasterMock = { success: vi.fn(), error: vi.fn() };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
  });

  it('loads categories and default Sales tax lines on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(categoryServiceMock.getList).toHaveBeenCalledTimes(1);
    expect(categoryServiceMock.getDefaultTaxLines).toHaveBeenCalledWith('Sales');
    expect(comp.defaultTaxLines()).toHaveLength(1);
    expect(comp.defaultTaxLines()[0].taxCategoryCode).toBe('SST10');
  });

  it('switches default tax lines preview to Purchase', () => {
    categoryServiceMock.getDefaultTaxLines.mockReturnValue(of([
      { taxName: 'Input Tax (6%)', rate: 6, chargeType: 'OnNetTotal', taxCategoryCode: 'SVC6' },
    ]));
    const comp = createComponent();

    comp.loadDefaultTaxLines('Purchase');

    expect(comp.previewTransactionType()).toBe('Purchase');
    expect(categoryServiceMock.getDefaultTaxLines).toHaveBeenCalledWith('Purchase');
    expect(comp.defaultTaxLines()[0].rate).toBe(6);
    expect(comp.isLoadingDefaultLines()).toBe(false);
  });

  it('resets loading flag on default tax lines error', () => {
    categoryServiceMock.getDefaultTaxLines.mockReturnValue(throwError(() => new Error('boom')));
    const comp = createComponent();

    comp.loadDefaultTaxLines('Sales');

    expect(comp.isLoadingDefaultLines()).toBe(false);
  });

  it('uses numeric TaxType enum default and resolves TaxType labels', () => {
    const comp = createComponent();

    expect(comp.categoryForm.controls.taxType.value).toBe(TaxType.Sales);
    expect(comp.getTaxTypeLabel(TaxType.Service)).toBe('Service');
    expect(comp.getTaxTypeLabel(TaxType.ZeroRated)).toBe('ZeroRated');
    expect(comp.getTaxTypeLabel('Exempt')).toBe('Exempt');
  });

  it('saves valid category with numeric TaxType and refreshes default tax lines', () => {
    const comp = createComponent();
    comp.categoryForm.patchValue({
      code: 'SVC6',
      name: 'Service Tax',
      taxType: TaxType.Service,
      isActive: true,
    });

    comp.saveCategory();

    expect(categoryServiceMock.create).toHaveBeenCalledWith(expect.objectContaining({
      code: 'SVC6',
      name: 'Service Tax',
      taxType: TaxType.Service,
    }));
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyCreated');
    expect(comp.categoryForm.controls.taxType.value).toBe(TaxType.Sales);
    expect(categoryServiceMock.getDefaultTaxLines).toHaveBeenCalled();
  });
});
