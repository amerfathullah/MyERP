import type {
  StockQtyVsSerialNoCountReportDto,
  StockQtyVsSerialNoCountFilterDto,
  SyncSerialNoStatusInputDto,
  SyncSerialNoStatusResultDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockQtyVsSerialNoCountService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: StockQtyVsSerialNoCountFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockQtyVsSerialNoCountReportDto>({
      method: 'GET',
      url: '/api/app/stock-qty-vs-serial-no-count/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemId: input.itemId,
        showDisabledItems: input.showDisabledItems,
        showOnlyDifferences: input.showOnlyDifferences,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });

  syncSerialNoStatus = (input: SyncSerialNoStatusInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SyncSerialNoStatusResultDto>({
      method: 'POST',
      url: '/api/app/stock-qty-vs-serial-no-count/sync-serial-no-status',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
