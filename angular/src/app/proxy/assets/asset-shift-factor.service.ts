import type { AssetShiftFactorDto, CreateUpdateAssetShiftFactorDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetShiftFactorService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateAssetShiftFactorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftFactorDto>({
      method: 'POST',
      url: '/api/app/asset-shift-factor',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-shift-factor/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftFactorDto>({
      method: 'GET',
      url: `/api/app/asset-shift-factor/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetShiftFactorDto>>({
      method: 'GET',
      url: '/api/app/asset-shift-factor',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateAssetShiftFactorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftFactorDto>({
      method: 'PUT',
      url: `/api/app/asset-shift-factor/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
