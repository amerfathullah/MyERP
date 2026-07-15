import type { AssetRepairDto, CreateAssetRepairDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AssetRepairService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'POST',
      url: `/api/app/asset-repair/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'POST',
      url: `/api/app/asset-repair/${id}/complete`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateAssetRepairDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'POST',
      url: '/api/app/asset-repair',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'GET',
      url: `/api/app/asset-repair/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetRepairDto>>({
      method: 'GET',
      url: '/api/app/asset-repair',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}