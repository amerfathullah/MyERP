import type { AssetMaintenanceDto, CreateUpdateAssetMaintenanceDto, GetAssetMaintenanceListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetMaintenanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateAssetMaintenanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceDto>({
      method: 'POST',
      url: '/api/app/asset-maintenance',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-maintenance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceDto>({
      method: 'GET',
      url: `/api/app/asset-maintenance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByAsset = (assetId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceDto>({
      method: 'GET',
      url: `/api/app/asset-maintenance/by-asset/${assetId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAssetMaintenanceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetMaintenanceDto>>({
      method: 'GET',
      url: '/api/app/asset-maintenance',
      params: { companyId: input.companyId, assetId: input.assetId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAssetMaintenanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceDto>({
      method: 'PUT',
      url: `/api/app/asset-maintenance/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}