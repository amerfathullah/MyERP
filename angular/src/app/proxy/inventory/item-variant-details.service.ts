import type { ItemVariantDetailsReportDto, ItemVariantDetailsFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemVariantDetailsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ItemVariantDetailsFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemVariantDetailsReportDto>({
      method: 'GET',
      url: '/api/app/item-variant-details/report',
      params: {
        templateItemId: input.templateItemId,
        warehouseId: input.warehouseId,
      },
    },
    { apiName: this.apiName, ...config });
}
