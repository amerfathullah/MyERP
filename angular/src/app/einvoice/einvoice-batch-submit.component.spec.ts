import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { EInvoiceBatchSubmitComponent } from './einvoice-batch-submit.component';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';
import { SalesInvoiceService } from '../proxy/sales/sales-invoice.service';
import { PurchaseInvoiceService } from '../proxy/purchasing/purchase-invoice.service';
import { CompanyContextService } from '../shared/services/company-context.service';

describe('EInvoiceBatchSubmitComponent', () => {
  function createComponent(options?: { loadError?: boolean }) {
    const salesInvoices = [
      { id: 'si-posted', invoiceNumber: 'SI-1', status: 'Posted', eInvoiceStatus: 'NotSubmitted', isReturn: false, isDebitNote: false, customerName: 'Acme' },
      { id: 'si-valid', invoiceNumber: 'SI-2', status: 'Posted', eInvoiceStatus: 'Valid', isReturn: false, isDebitNote: false, customerName: 'Acme' },
      { id: 'si-draft', invoiceNumber: 'SI-3', status: 'Draft', eInvoiceStatus: 'NotSubmitted', isReturn: false, isDebitNote: false, customerName: 'Acme' },
      { id: 'si-cn', invoiceNumber: 'CN-1', status: 'Posted', eInvoiceStatus: 'Rejected', isReturn: true, isDebitNote: false, customerName: 'Acme' },
      { id: 'si-dn', invoiceNumber: 'DN-1', status: 'Submitted', eInvoiceStatus: 'Failed', isReturn: false, isDebitNote: true, customerName: 'Acme' },
      { id: 'si-cons', invoiceNumber: 'SI-4', status: 'Posted', eInvoiceStatus: 'NotSubmitted', isReturn: false, isDebitNote: false, consolidatedSalesInvoiceId: 'c-1' },
    ];
    const purchaseInvoices = [
      { id: 'pi-1', invoiceNumber: 'PI-1', status: 'Posted', eInvoiceStatus: 'NotSubmitted', supplierName: 'Vendor Sdn Bhd' },
      { id: 'pi-valid', invoiceNumber: 'PI-2', status: 'Posted', eInvoiceStatus: 'Valid', supplierName: 'Vendor Sdn Bhd' },
    ];

    const eiMock = {
      batchSubmit: vi.fn().mockReturnValue(of({ succeededCount: 1, failedCount: 0, skippedCount: 0, results: [] })),
    };
    const siMock = {
      getList: options?.loadError
        ? vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Server unavailable' } } })))
        : vi.fn().mockReturnValue(of({ items: salesInvoices })),
    };
    const piMock = {
      getList: vi.fn().mockReturnValue(of({ items: purchaseInvoices })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: EInvoiceService, useValue: eiMock },
        { provide: SalesInvoiceService, useValue: siMock },
        { provide: PurchaseInvoiceService, useValue: piMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new EInvoiceBatchSubmitComponent());
    return { comp, eiMock, siMock, piMock, toasterMock };
  }

  it('loads Posted and Submitted non-consolidated sales invoices and skips Draft', () => {
    const { comp, siMock } = createComponent();

    comp.loadInvoices();

    expect(siMock.getList).toHaveBeenCalledWith(expect.not.objectContaining({ status: 'Submitted' }));
    expect(comp.invoices().map(i => i.id)).toEqual(['si-posted', 'si-valid']);
  });

  it('selectAll selects only selectable invoices and skips Valid', () => {
    const { comp } = createComponent();
    comp.loadInvoices();

    comp.selectAll();

    expect(comp.selected()).toEqual(['si-posted']);
    expect(comp.allSelected()).toBe(true);
  });

  it('filters CreditNote rows and submits with SalesInvoice sourceDocumentType', () => {
    const { comp, eiMock } = createComponent();
    comp.docType = 'CreditNote';
    comp.loadInvoices();

    expect(comp.invoices().map(i => i.id)).toEqual(['si-cn']);

    comp.selectAll();
    comp.submitBatch();

    expect(eiMock.batchSubmit).toHaveBeenCalledWith({
      companyId: 'comp-1',
      sourceDocumentType: 'SalesInvoice',
      documentIds: ['si-cn'],
    });
  });

  it('loads and submits Self-Billed PurchaseInvoices via PurchaseInvoiceService', () => {
    const { comp, piMock, eiMock } = createComponent();
    comp.docType = 'PurchaseInvoice';
    comp.loadInvoices();

    expect(piMock.getList).toHaveBeenCalled();
    expect(comp.invoices().map(i => i.id)).toEqual(['pi-1', 'pi-valid']);

    comp.selectAll();
    comp.submitBatch();

    expect(comp.selected()).toEqual(['pi-1']);
    expect(eiMock.batchSubmit).toHaveBeenCalledWith({
      companyId: 'comp-1',
      sourceDocumentType: 'PurchaseInvoice',
      documentIds: ['pi-1'],
    });
  });

  it('shows toaster error when loading invoices fails', () => {
    const { comp, toasterMock } = createComponent({ loadError: true });

    comp.loadInvoices();

    expect(toasterMock.error).toHaveBeenCalledWith('Server unavailable');
    expect(comp.loadingInvoices()).toBe(false);
  });
});
