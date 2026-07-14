import type { CreateRoutingDto, RoutingDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class RoutingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateRoutingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RoutingDto>({
      method: 'POST',
      url: '/api/app/routing',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RoutingDto>>({
      method: 'GET',
      url: '/api/app/routing',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}