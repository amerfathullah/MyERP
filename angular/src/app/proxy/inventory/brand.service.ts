import type { BrandDto, CreateUpdateBrandDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BrandService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateBrandDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandDto>({
      method: 'POST',
      url: '/api/app/brand',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/brand/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandDto>({
      method: 'GET',
      url: `/api/app/brand/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BrandDto>>({
      method: 'GET',
      url: '/api/app/brand',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateBrandDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandDto>({
      method: 'PUT',
      url: `/api/app/brand/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
