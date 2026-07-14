import type { CreatePickListDto, PickListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PickListService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'POST',
      url: `/api/app/pick-list/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePickListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'POST',
      url: '/api/app/pick-list',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'GET',
      url: `/api/app/pick-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PickListDto>>({
      method: 'GET',
      url: '/api/app/pick-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'POST',
      url: `/api/app/pick-list/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}