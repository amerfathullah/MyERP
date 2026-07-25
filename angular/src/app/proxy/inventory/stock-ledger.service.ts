import type { StockLedgerReportDto, StockLedgerRequestDto, VoucherStockLedgerDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockLedgerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getForVoucher = (voucherType: string, voucherId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VoucherStockLedgerDto>({
      method: 'GET',
      url: `/api/app/stock-ledger/for-voucher/${voucherId}`,
      params: { voucherType },
    },
    { apiName: this.apiName,...config });
  

  getStockLedger = (input: StockLedgerRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockLedgerReportDto>({
      method: 'GET',
      url: '/api/app/stock-ledger/stock-ledger',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, itemId: input.itemId, warehouseId: input.warehouseId },
    },
    { apiName: this.apiName,...config });
}