import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { EinvoiceStatusReportComponent } from './einvoice-status-report.component';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('EinvoiceStatusReportComponent', () => {
  function createComponent(companyId = 'comp-1') {
    const einvoiceServiceMock = {
      getSalesStatusReport: vi.fn().mockReturnValue(of([])),
      getPurchaseStatusReport: vi.fn().mockReturnValue(of([])),
      getVatReport: vi.fn().mockReturnValue(of({ totalOutputTax: 0, totalInputTax: 0 })),
    };

    const injector = Injector.create({
      providers: [
        { provide: EInvoiceService, useValue: einvoiceServiceMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => companyId } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new EinvoiceStatusReportComponent());
    return { comp, einvoiceServiceMock };
  }

  it('passes active companyId to sales, purchase, and VAT report queries', () => {
    const { comp, einvoiceServiceMock } = createComponent('comp-42');

    comp.generate();
    expect(einvoiceServiceMock.getSalesStatusReport).toHaveBeenCalledWith(
      expect.objectContaining({ companyId: 'comp-42' })
    );

    comp.filters.patchValue({ reportType: 'purchase' });
    comp.generate();
    expect(einvoiceServiceMock.getPurchaseStatusReport).toHaveBeenCalledWith(
      expect.objectContaining({ companyId: 'comp-42' })
    );

    comp.filters.patchValue({ reportType: 'vat' });
    comp.generate();
    expect(einvoiceServiceMock.getVatReport).toHaveBeenCalledWith(
      expect.objectContaining({ companyId: 'comp-42' })
    );
  });
});
