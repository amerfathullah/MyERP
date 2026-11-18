import type {
  IncorrectBalanceQtyReportDto,
  IncorrectBalanceQtyRequestDto,
  IncorrectStockValueReportDto,
  IncorrectStockValueRequestDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockDiagnosticsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getIncorrectBalanceQty = (
    input: IncorrectBalanceQtyRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectBalanceQtyReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-balance-qty',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          precision: input.precision,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIncorrectStockValue = (
    input: IncorrectStockValueRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectStockValueReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-stock-value',
        params: {
          companyId: input.companyId,
          accountId: input.accountId,
          fromDate: input.fromDate,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
