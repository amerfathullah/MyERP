import type { AssetMovementDto, CreateUpdateAssetMovementDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetMovementService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'POST',
      url: `/api/app/asset-movement/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateAssetMovementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'POST',
      url: '/api/app/asset-movement',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-movement/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'GET',
      url: `/api/app/asset-movement/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetMovementDto>>({
      method: 'GET',
      url: '/api/app/asset-movement',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'POST',
      url: `/api/app/asset-movement/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAssetMovementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'PUT',
      url: `/api/app/asset-movement/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}