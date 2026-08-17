import type { CreateItemTaxTemplateDto, ItemTaxTemplateDto, UpdateItemTaxTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemTaxTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateItemTaxTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemTaxTemplateDto>({
      method: 'POST',
      url: '/api/app/item-tax-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/item-tax-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemTaxTemplateDto>({
      method: 'GET',
      url: `/api/app/item-tax-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ItemTaxTemplateDto>>({
      method: 'GET',
      url: '/api/app/item-tax-template',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateItemTaxTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemTaxTemplateDto>({
      method: 'PUT',
      url: `/api/app/item-tax-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}