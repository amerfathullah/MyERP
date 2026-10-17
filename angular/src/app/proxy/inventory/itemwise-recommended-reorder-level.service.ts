import type { ItemwiseRecommendedReorderLevelFilterDto, ItemwiseRecommendedReorderLevelReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemwiseRecommendedReorderLevelService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ItemwiseRecommendedReorderLevelFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemwiseRecommendedReorderLevelReportDto>({
      method: 'GET',
      url: '/api/app/itemwise-recommended-reorder-level/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        itemGroupId: input.itemGroupId,
        brand: input.brand,
        searchText: input.searchText,
      },
    },
    { apiName: this.apiName, ...config });
}
