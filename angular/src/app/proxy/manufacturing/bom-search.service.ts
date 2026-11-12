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
    this.restService.request<any, BomSearchReportDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/bom-search/search',
      body: input,
    },
      { apiName: this.apiName,...config });
}
