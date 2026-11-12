import type { StockProjectedQtyFilterDto, StockProjectedQtyReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockProjectedQtyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: StockProjectedQtyFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockProjectedQtyReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/stock-projected-qty/report',
      params: { companyId: input.companyId, warehouseId: input.warehouseId, itemId: input.itemId, itemGroupId: input.itemGroupId, brand: input.brand, showOnlyShortage: input.showOnlyShortage, searchTerm: input.searchTerm },
    },
      { apiName: this.apiName,...config });
}
