import type { AssetActivityDto, CreateAssetActivityDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetActivityService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAssetActivityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetActivityDto>({
      method: 'POST',
      url: '/api/app/asset-activity',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getListByAsset = (assetId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetActivityDto[]>({
      method: 'GET',
      url: `/api/app/asset-activity/by-asset/${assetId}`,
    },
    { apiName: this.apiName,...config });
}