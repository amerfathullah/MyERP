import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { BlanketOrderDetailComponent } from './blanket-order-detail.component';
import { BlanketOrderService } from '../../proxy/sales/blanket-order.service';

describe('BlanketOrderDetailComponent error handling', () => {
  it('surfaces toaster error when load, submit, close, reopen, cancel, closeItem, or reopenItem fails', () => {
    const serviceMock = {
      get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket load failed' } } }))),
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket submit failed' } } }))),
      close: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket close failed' } } }))),
      reopen: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket reopen failed' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket cancel failed' } } }))),
      closeItem: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket closeItem failed' } } }))),
      reopenItem: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Blanket reopenItem failed' } } }))),
    };
    const toasterMock = { error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'bo-1' } } } },
        { provide: BlanketOrderService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new BlanketOrderDetailComponent());
    comp.ngOnInit();
    expect(comp.isLoading).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket load failed');

    comp.order = { id: 'bo-1', status: 0 };
    comp.submit();
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket submit failed');

    comp.close();
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket close failed');

    comp.reopen();
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket reopen failed');

    comp.cancel();
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket cancel failed');

    comp.closeItem('item-1');
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket closeItem failed');

    comp.reopenItem('item-1');
    expect(toasterMock.error).toHaveBeenCalledWith('Blanket reopenItem failed');
  });
});
