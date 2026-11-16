import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { StockClosingDetailComponent } from './stock-closing-detail.component';
import { StockClosingService } from '../../proxy/inventory/stock-closing.service';

describe('StockClosingDetailComponent Regenerate', () => {
  function createComponent(confirmStatus = Confirmation.Status.confirm) {
    const initial = { id: 'sc-1', status: 0, totalEntries: 12, totalStockValue: 5000 };
    const regenerated = { id: 'sc-1', status: 0, totalEntries: 15, totalStockValue: 6200 };
    const closingMock = {
      get: vi.fn().mockReturnValue(of(initial)),
      regenerate: vi.fn().mockReturnValue(of(regenerated)),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(confirmStatus)) };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'sc-1' } } } },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: StockClosingService, useValue: closingMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockClosingDetailComponent());
    comp.ngOnInit();
    return { comp, closingMock, toasterMock };
  }

  it('calls regenerate on draft entry only after confirmation', () => {
    const cancelled = createComponent(Confirmation.Status.dismiss);
    cancelled.comp.regenerate();
    expect(cancelled.closingMock.regenerate).not.toHaveBeenCalled();

    const confirmed = createComponent(Confirmation.Status.confirm);
    confirmed.comp.regenerate();
    expect(confirmed.closingMock.regenerate).toHaveBeenCalledWith('sc-1');
    expect(confirmed.toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
  });
});
