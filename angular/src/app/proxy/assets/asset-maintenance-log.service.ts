import type { AssetMaintenanceLogDto, CompleteAssetMaintenanceLogDto, CreateUpdateAssetMaintenanceLogDto, GetAssetMaintenanceLogListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetMaintenanceLogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceLogDto>({
      method: 'POST',
      url: `/api/app/asset-maintenance-log/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  complete = (id: string, input: CompleteAssetMaintenanceLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceLogDto>({
      method: 'POST',
      url: `/api/app/asset-maintenance-log/${id}/complete`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateAssetMaintenanceLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceLogDto>({
      method: 'POST',
      url: '/api/app/asset-maintenance-log',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-maintenance-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceLogDto>({
      method: 'GET',
      url: `/api/app/asset-maintenance-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAssetMaintenanceLogListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetMaintenanceLogDto>>({
      method: 'GET',
      url: '/api/app/asset-maintenance-log',
      params: { companyId: input.companyId, assetId: input.assetId, assetMaintenanceId: input.assetMaintenanceId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLogsByAsset = (assetId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceLogDto[]>({
      method: 'GET',
      url: `/api/app/asset-maintenance-log/logs-by-asset/${assetId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAssetMaintenanceLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceLogDto>({
      method: 'PUT',
      url: `/api/app/asset-maintenance-log/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}