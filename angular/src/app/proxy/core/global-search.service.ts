import type { GlobalSearchInput, SearchResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GlobalSearchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  search = (input: GlobalSearchInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SearchResultDto[]>({
      method: 'POST',
      url: '/api/app/global-search/search',
      body: input,
    },
    { apiName: this.apiName,...config });
}