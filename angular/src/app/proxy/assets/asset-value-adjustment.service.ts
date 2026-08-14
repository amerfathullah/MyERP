import type { AssetValueAdjustmentDto, CreateUpdateAssetValueAdjustmentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetValueAdjustmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetValueAdjustmentDto>({
      method: 'POST',
      url: `/api/app/asset-value-adjustment/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateAssetValueAdjustmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetValueAdjustmentDto>({
      method: 'POST',
      url: '/api/app/asset-value-adjustment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-value-adjustment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetValueAdjustmentDto>({
      method: 'GET',
      url: `/api/app/asset-value-adjustment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetValueAdjustmentDto>>({
      method: 'GET',
      url: '/api/app/asset-value-adjustment',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetValueAdjustmentDto>({
      method: 'POST',
      url: `/api/app/asset-value-adjustment/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAssetValueAdjustmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetValueAdjustmentDto>({
      method: 'PUT',
      url: `/api/app/asset-value-adjustment/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}