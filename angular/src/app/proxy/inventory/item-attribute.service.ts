import type { CreateItemAttributeDto, ItemAttributeDto, ItemAttributeValueDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemAttributeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addValue = (id: string, input: ItemAttributeValueDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAttributeDto>({
      method: 'POST',
      url: `/api/app/item-attribute/${id}/value`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateItemAttributeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAttributeDto>({
      method: 'POST',
      url: '/api/app/item-attribute',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-attribute/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAttributeDto>({
      method: 'GET',
      url: `/api/app/item-attribute/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemAttributeDto[]>({
      method: 'GET',
      url: '/api/app/item-attribute',
    },
    { apiName: this.apiName,...config });
}