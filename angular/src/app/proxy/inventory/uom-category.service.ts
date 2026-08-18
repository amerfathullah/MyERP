import type { CreateUpdateUomCategoryDto, UomCategoryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UomCategoryService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateUomCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UomCategoryDto>({
      method: 'POST',
      url: '/api/app/uom-category',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/uom-category/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UomCategoryDto>({
      method: 'GET',
      url: `/api/app/uom-category/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<UomCategoryDto>>({
      method: 'GET',
      url: '/api/app/uom-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateUomCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UomCategoryDto>({
      method: 'PUT',
      url: `/api/app/uom-category/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
