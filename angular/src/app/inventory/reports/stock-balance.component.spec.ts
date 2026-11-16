import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { StockBalanceComponent } from './stock-balance.component';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';

describe('StockBalanceComponent', () => {
  let serviceMock: any;
  let warehouseMock: any;
  let confirmationMock: any;
  let toasterMock: any;

  function createComponent(): StockBalanceComponent {
    const injector = Injector.create({
      providers: [
        { provide: StockBalanceService, useValue: serviceMock },
        { provide: WarehouseService, useValue: warehouseMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });
    return runInInjectionContext(injector, () => new StockBalanceComponent());
  }

  beforeEach(() => {
    serviceMock = {
      getStockBalance: vi.fn().mockReturnValue(of({ items: [{ id: 'b1', itemId: 'i1', warehouseId: 'w1', actualQty: 10 }], totalCount: 1 })),
      recalculateBinQty: vi.fn().mockReturnValue(of(void 0)),
    };
    warehouseMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
    };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
    toasterMock = { success: vi.fn(), error: vi.fn() };
  });

  it('recalculates bin qty on confirmation and reloads stock balance', () => {
    const comp = createComponent();

    comp.recalculateBin({ itemId: 'i1', warehouseId: 'w1' });

    expect(confirmationMock.warn).toHaveBeenCalledWith('::RecalculateBinConfirmation', '::AreYouSure');
    expect(serviceMock.recalculateBinQty).toHaveBeenCalledWith('i1', 'w1');
    expect(comp.recalculatingKey()).toBeNull();
    expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
    expect(serviceMock.getStockBalance).toHaveBeenCalledTimes(1);
  });

  it('skips recalculateBin when itemId or warehouseId missing or cancelled', () => {
    const comp = createComponent();
    comp.recalculateBin({ itemId: '', warehouseId: 'w1' });
    expect(confirmationMock.warn).not.toHaveBeenCalled();

    confirmationMock.warn.mockReturnValue(of(Confirmation.Status.reject));
    comp.recalculateBin({ itemId: 'i1', warehouseId: 'w1' });
    expect(serviceMock.recalculateBinQty).not.toHaveBeenCalled();
  });

  it('clears recalculatingKey and shows error toast when recalculateBinQty fails', () => {
    serviceMock.recalculateBinQty.mockReturnValue(
      throwError(() => ({ error: { error: { message: 'Ledger lock timeout' } } }))
    );
    const comp = createComponent();

    comp.recalculateBin({ itemId: 'i1', warehouseId: 'w1' });

    expect(comp.recalculatingKey()).toBeNull();
    expect(toasterMock.error).toHaveBeenCalledWith('Ledger lock timeout');
  });
});
