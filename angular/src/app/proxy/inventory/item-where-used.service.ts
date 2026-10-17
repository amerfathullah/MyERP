import type { ItemWhereUsedRequestDto, ItemWhereUsedReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemWhereUsedService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ItemWhereUsedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemWhereUsedReportDto>({
      method: 'GET',
      url: '/api/app/item-where-used/report',
      params: {
        itemId: input.itemId,
        companyId: input.companyId,
        section: input.section,
      },
    },
    { apiName: this.apiName, ...config });
}
