import type { ItemMovementHistoryDto, StockLedgerReportDto, StockLedgerRequestDto, StockMovementSummaryDto, VoucherStockLedgerDto } from './models';
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
  

  getItemMovementHistory = (itemId: string, warehouseId?: string, maxEntries: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemMovementHistoryDto>({
      method: 'GET',
      url: '/api/app/stock-ledger/item-movement-history',
      params: { itemId, warehouseId, maxEntries },
    },
    { apiName: this.apiName,...config });
  

  getStockLedger = (input: StockLedgerRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockLedgerReportDto>({
      method: 'GET',
      url: '/api/app/stock-ledger/stock-ledger',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, itemId: input.itemId, warehouseId: input.warehouseId },
    },
    { apiName: this.apiName,...config });
  

  getStockMovementSummary = (companyId: string, fromDate: string, toDate: string, warehouseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockMovementSummaryDto>({
      method: 'GET',
      url: '/api/app/stock-ledger/stock-movement-summary',
      params: { companyId, fromDate, toDate, warehouseId },
    },
    { apiName: this.apiName,...config });
}