import type { AssetShiftAllocationDto, CreateAssetShiftAllocationDto, DepreciationScheduleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetShiftAllocationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftAllocationDto>({
      method: 'POST',
      url: `/api/app/asset-shift-allocation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateAssetShiftAllocationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftAllocationDto>({
      method: 'POST',
      url: '/api/app/asset-shift-allocation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftAllocationDto>({
      method: 'GET',
      url: `/api/app/asset-shift-allocation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetShiftAllocationDto>>({
      method: 'GET',
      url: '/api/app/asset-shift-allocation',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getUnbookedSchedule = (assetId: string, financeBookId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DepreciationScheduleDto>>({
      method: 'GET',
      url: '/api/app/asset-shift-allocation/unbooked-schedule',
      params: { assetId, financeBookId },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetShiftAllocationDto>({
      method: 'POST',
      url: `/api/app/asset-shift-allocation/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}