import type { AssetCategoryDto, AssetDto, CreateAssetDto, CreateUpdateAssetCategoryDto, GetAssetListDto, UpdateAssetDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAssetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({
      method: 'POST',
      url: '/api/app/asset',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createCategory = (input: CreateUpdateAssetCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCategoryDto>({
      method: 'POST',
      url: '/api/app/asset/category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({
      method: 'GET',
      url: `/api/app/asset/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCategories = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetCategoryDto[]>({
      method: 'GET',
      url: '/api/app/asset/categories',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAssetListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetDto>>({
      method: 'GET',
      url: '/api/app/asset',
      params: { status: input.status, filter: input.filter, companyId: input.companyId, assetCategoryId: input.assetCategoryId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  scrap = (id: string, disposalDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({
      method: 'POST',
      url: `/api/app/asset/${id}/scrap`,
      params: { disposalDate },
    },
    { apiName: this.apiName,...config });
  

  sell = (id: string, disposalDate: string, amount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({
      method: 'POST',
      url: `/api/app/asset/${id}/sell`,
      params: { disposalDate, amount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({
      method: 'POST',
      url: `/api/app/asset/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateAssetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({
      method: 'PUT',
      url: `/api/app/asset/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}