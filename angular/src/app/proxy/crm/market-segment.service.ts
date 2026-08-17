import type { CreateUpdateMarketSegmentDto, GetMarketSegmentListDto, MarketSegmentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MarketSegmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateMarketSegmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MarketSegmentDto>({
      method: 'POST',
      url: '/api/app/market-segment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/market-segment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MarketSegmentDto>({
      method: 'GET',
      url: `/api/app/market-segment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetMarketSegmentListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MarketSegmentDto>>({
      method: 'GET',
      url: '/api/app/market-segment',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateMarketSegmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MarketSegmentDto>({
      method: 'PUT',
      url: `/api/app/market-segment/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}