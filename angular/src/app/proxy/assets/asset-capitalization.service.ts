import type { AssetCapitalizationDto, CreateAssetCapitalizationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AssetCapitalizationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/asset-capitalization/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateAssetCapitalizationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCapitalizationDto>({
      method: 'POST',
      url: '/api/app/asset-capitalization',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCapitalizationDto>({
      method: 'GET',
      url: `/api/app/asset-capitalization/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetCapitalizationDto>>({
      method: 'GET',
      url: '/api/app/asset-capitalization',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/asset-capitalization/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}