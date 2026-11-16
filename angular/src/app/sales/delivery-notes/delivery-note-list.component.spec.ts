import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { DeliveryNoteListComponent } from './delivery-note-list.component';
import { DeliveryNoteStore } from '../store/delivery-note.store';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';

describe('DeliveryNoteListComponent', () => {
  let storeMock: any;
  let dnServiceMock: any;
  let toasterMock: any;

  function createComponent(): DeliveryNoteListComponent {
    const injector = Injector.create({
      providers: [
        { provide: DeliveryNoteStore, useValue: storeMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: signal('comp-1') } },
        { provide: SalesInvoiceService, useValue: { createFromDeliveryNotes: vi.fn() } },
        { provide: DeliveryNoteService, useValue: dnServiceMock },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });
    return runInInjectionContext(injector, () => new DeliveryNoteListComponent());
  }

  beforeEach(() => {
    storeMock = {
      entities: signal([]),
      totalCount: signal(0),
      isLoading: signal(false),
      load: vi.fn(),
    };
    dnServiceMock = {
      recalculateReturnedDeliveryNotesBillingStatus: vi.fn().mockReturnValue(of(3)),
    };
    toasterMock = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warn: vi.fn() };
  });

  it('recalculates returned delivery notes billing status and reloads list', () => {
    const comp = createComponent();

    comp.recalculateReturnedBillingStatus();

    expect(dnServiceMock.recalculateReturnedDeliveryNotesBillingStatus).toHaveBeenCalledTimes(1);
    expect(comp.isRecalculatingBilling()).toBe(false);
    expect(toasterMock.success).toHaveBeenCalledWith('::ReturnedBillingUpdated (3)');
    expect(storeMock.load).toHaveBeenCalledTimes(1);
  });

  it('resets loading flag and shows error toast on failure', () => {
    dnServiceMock.recalculateReturnedDeliveryNotesBillingStatus.mockReturnValue(
      throwError(() => ({ error: { error: { message: 'Recalc failed' } } }))
    );
    const comp = createComponent();

    comp.recalculateReturnedBillingStatus();

    expect(comp.isRecalculatingBilling()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Recalc failed');
  });
});
