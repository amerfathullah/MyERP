import type { ItemShortageReportDto, ItemShortageReportRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemShortageReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: ItemShortageReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemShortageReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/item-shortage-report/report',
      params: { companyId: input.companyId, warehouseId: input.warehouseId, searchText: input.searchText },
    },
      { apiName: this.apiName,...config });
}
