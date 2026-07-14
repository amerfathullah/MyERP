import type { CreateProductBundleDto, ProductBundleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProductBundleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateProductBundleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductBundleDto>({
      method: 'POST',
      url: '/api/app/product-bundle',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deactivate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductBundleDto>({
      method: 'POST',
      url: `/api/app/product-bundle/${id}/deactivate`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProductBundleDto>>({
      method: 'GET',
      url: '/api/app/product-bundle',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}