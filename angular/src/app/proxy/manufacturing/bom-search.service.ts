import type { BomSearchFilterDto, BomSearchReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomSearchService {
  private restService = inject(RestService);
  apiName = 'Default';

  search = (input: BomSearchFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomSearchReportDto>(
      {
        method: 'GET',
        url: '/api/app/bom-search/search',
        params: {
          companyId: input.companyId,
          item1Id: input.item1Id,
          item2Id: input.item2Id,
          item3Id: input.item3Id,
          item4Id: input.item4Id,
          item5Id: input.item5Id,
          searchSubAssemblies: input.searchSubAssemblies,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
