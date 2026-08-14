import type { CreateUpdateItemAlternativeDto, ItemAlternativeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemAlternativeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateItemAlternativeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAlternativeDto>({
      method: 'POST',
      url: '/api/app/item-alternative',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-alternative/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAlternativeDto>({
      method: 'GET',
      url: `/api/app/item-alternative/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAlternatives = (itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAlternativeDto[]>({
      method: 'GET',
      url: `/api/app/item-alternative/alternatives/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemAlternativeDto>>({
      method: 'GET',
      url: '/api/app/item-alternative',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateItemAlternativeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAlternativeDto>({
      method: 'PUT',
      url: `/api/app/item-alternative/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}