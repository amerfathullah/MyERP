import { Injector, runInInjectionContext } from '@angular/core';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { EInvoiceListComponent } from './einvoice-list.component';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';

describe('EInvoiceListComponent', () => {
  function createComponent(options?: { cancelError?: boolean }) {
    const initialItem = {
      id: 'sub-1',
      sourceDocumentType: 'SalesInvoice',
      sourceDocumentId: 'si-1',
      status: 'Valid',
      documentUuid: 'UUID-1234',
    };
    const cancelledItem = { ...initialItem, status: 'Cancelled' };

    const serviceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [initialItem], totalCount: 1 })),
      getStatus: vi.fn(),
      cancel: options?.cancelError
        ? vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: '72h window expired' } } })))
        : vi.fn().mockReturnValue(of(cancelledItem)),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: EInvoiceService, useValue: serviceMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new EInvoiceListComponent());
    comp.ngOnInit();
    return { comp, serviceMock, toasterMock, initialItem };
  }

  it('requires non-empty cancellation reason before calling cancel API', () => {
    const { comp, serviceMock, initialItem } = createComponent();
    comp.openCancelModal(initialItem as any);
    comp.cancelReason.set('   ');

    comp.confirmCancel();

    expect(serviceMock.cancel).not.toHaveBeenCalled();
  });

  it('submits trimmed LHDN cancellation reason and updates submission status', () => {
    const { comp, serviceMock, toasterMock, initialItem } = createComponent();
    comp.openCancelModal(initialItem as any);
    comp.cancelReason.set('  Wrong buyer details  ');

    comp.confirmCancel();

    expect(serviceMock.cancel).toHaveBeenCalledWith({
      submissionId: 'sub-1',
      reason: 'Wrong buyer details',
    });
    expect(comp.cancelTarget()).toBeNull();
    expect(comp.items()[0].status).toBe('Cancelled');
    expect(toasterMock.success).toHaveBeenCalled();
  });

  it('surfaces backend error and stops spinner when cancellation fails', () => {
    const { comp, toasterMock, initialItem } = createComponent({ cancelError: true });
    comp.openCancelModal(initialItem as any);
    comp.cancelReason.set('Duplicate submission');

    comp.confirmCancel();

    expect(comp.isCancelling()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('72h window expired');
  });
});
