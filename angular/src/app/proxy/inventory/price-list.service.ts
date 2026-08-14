import type { CreateUpdatePriceListDto, PriceListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PriceListService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePriceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PriceListDto>({
      method: 'POST',
      url: '/api/app/price-list',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/price-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PriceListDto>({
      method: 'GET',
      url: `/api/app/price-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PriceListDto>>({
      method: 'GET',
      url: '/api/app/price-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePriceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PriceListDto>({
      method: 'PUT',
      url: `/api/app/price-list/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}