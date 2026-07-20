import type { AssetCategoryDetailDto, CreateUpdateAssetCategoryDetailDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetCategoryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateAssetCategoryDetailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCategoryDetailDto>({
      method: 'POST',
      url: '/api/app/asset-category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCategoryDetailDto>({
      method: 'GET',
      url: `/api/app/asset-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetCategoryDetailDto>>({
      method: 'GET',
      url: '/api/app/asset-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAssetCategoryDetailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCategoryDetailDto>({
      method: 'PUT',
      url: `/api/app/asset-category/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}