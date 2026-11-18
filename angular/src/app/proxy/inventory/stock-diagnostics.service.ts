import type {
  FixSerialAndBatchBundlesInputDto,
  FixSerialAndBatchBundlesResultDto,
  IncorrectBalanceQtyReportDto,
  IncorrectBalanceQtyRequestDto,
  IncorrectSerialAndBatchBundleReportDto,
  IncorrectSerialAndBatchBundleRequestDto,
  IncorrectSerialNoValuationReportDto,
  IncorrectSerialNoValuationRequestDto,
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

  getIncorrectSerialNoValuation = (
    input: IncorrectSerialNoValuationRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectSerialNoValuationReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-serial-no-valuation',
        params: {
          companyId: input.companyId,
          itemId: input.itemId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getIncorrectSerialAndBatchBundles = (
    input: IncorrectSerialAndBatchBundleRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, IncorrectSerialAndBatchBundleReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/incorrect-serial-and-batch-bundles',
        params: {
          companyId: input.companyId,
          itemId: input.itemId,
          warehouseId: input.warehouseId,
          voucherType: input.voucherType,
          postingDate: input.postingDate,
        },
      },
      { apiName: this.apiName, ...config },
    );

  fixIncorrectSerialAndBatchBundles = (
    input: FixSerialAndBatchBundlesInputDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, FixSerialAndBatchBundlesResultDto>(
      {
        method: 'POST',
        headers: { Accept: 'application/json' },
        url: '/api/app/stock-diagnostics/fix-incorrect-serial-and-batch-bundles',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
