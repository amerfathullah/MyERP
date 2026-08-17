import type { AssetMaintenanceTeamDto, CreateUpdateAssetMaintenanceTeamDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface GetAssetMaintenanceTeamListDto {
  companyId?: string;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

@Injectable({
  providedIn: 'root',
})
export class AssetMaintenanceTeamService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateAssetMaintenanceTeamDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceTeamDto>({
      method: 'POST',
      url: '/api/app/asset-maintenance-team',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-maintenance-team/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceTeamDto>({
      method: 'GET',
      url: `/api/app/asset-maintenance-team/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetAssetMaintenanceTeamListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetMaintenanceTeamDto>>({
      method: 'GET',
      url: '/api/app/asset-maintenance-team',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateAssetMaintenanceTeamDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMaintenanceTeamDto>({
      method: 'PUT',
      url: `/api/app/asset-maintenance-team/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
