import type { ItemsToBeRequestedFilterDto, ItemsToBeRequestedReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemsToBeRequestedService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ItemsToBeRequestedFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemsToBeRequestedReportDto>({
      method: 'GET',
      url: '/api/app/items-to-be-requested/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemGroupId: input.itemGroupId,
        searchTerm: input.searchTerm
      },
    },
    { apiName: this.apiName,...config });
}
