import type { AssetMovementDto, CreateAssetMovementDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AssetMovementService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAssetMovementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'POST',
      url: '/api/app/asset-movement',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetMovementDto>>({
      method: 'GET',
      url: '/api/app/asset-movement',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetMovementDto>({
      method: 'POST',
      url: `/api/app/asset-movement/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}