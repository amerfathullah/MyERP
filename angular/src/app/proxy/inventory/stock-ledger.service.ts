import type { StockLedgerReportDto, StockLedgerRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockLedgerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getStockLedger = (input: StockLedgerRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockLedgerReportDto>({
      method: 'GET',
      url: '/api/app/stock-ledger/stock-ledger',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, itemId: input.itemId, warehouseId: input.warehouseId },
    },
    { apiName: this.apiName,...config });
}