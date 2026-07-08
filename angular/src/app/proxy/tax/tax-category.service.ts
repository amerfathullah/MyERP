import type { CreateUpdateTaxCategoryDto, TaxCategoryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxCategoryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateTaxCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxCategoryDto>({
      method: 'POST',
      url: '/api/app/tax-category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tax-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxCategoryDto>({
      method: 'GET',
      url: `/api/app/tax-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TaxCategoryDto>>({
      method: 'GET',
      url: '/api/app/tax-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateTaxCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxCategoryDto>({
      method: 'PUT',
      url: `/api/app/tax-category/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}