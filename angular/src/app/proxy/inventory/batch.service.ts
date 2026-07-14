import type { BatchDto, CreateBatchDto, GetBatchListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BatchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateBatchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchDto>({
      method: 'POST',
      url: '/api/app/batch',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/batch/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchDto>({
      method: 'GET',
      url: `/api/app/batch/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBatchListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BatchDto>>({
      method: 'GET',
      url: '/api/app/batch',
      params: { itemId: input.itemId, isDisabled: input.isDisabled, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}