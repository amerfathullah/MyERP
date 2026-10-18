import type {
  StockLedgerInvariantCheckFilterDto,
  StockLedgerInvariantCheckReportDto,
  StockLedgerVarianceFilterDto,
  StockLedgerVarianceReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockLedgerInvariantCheckService {
  private restService = inject(RestService);
  apiName = 'Default';

  getInvariantCheck = (input: StockLedgerInvariantCheckFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockLedgerInvariantCheckReportDto>(
      {
        method: 'GET',
        url: '/api/app/stock-ledger-invariant-check/invariant-check',
        params: {
          itemId: input.itemId,
          warehouseId: input.warehouseId,
          showIncorrectEntries: input.showIncorrectEntries,
        },
      },
      { apiName: this.apiName, ...config }
    );

  getStockLedgerVariance = (input: StockLedgerVarianceFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockLedgerVarianceReportDto>(
      {
        method: 'GET',
        url: '/api/app/stock-ledger-invariant-check/stock-ledger-variance',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          showOnlyViolations: input.showOnlyViolations,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
