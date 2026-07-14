import type { CreateDocumentSeriesDto, DocumentSeriesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentSeriesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateDocumentSeriesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentSeriesDto>({
      method: 'POST',
      url: '/api/app/document-series',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DocumentSeriesDto>>({
      method: 'GET',
      url: '/api/app/document-series',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}