import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { AssetDto, CreateAssetDto, UpdateAssetDto, AssetCategoryDto, CreateUpdateAssetCategoryDto } from './models';

@Injectable({ providedIn: 'root' })
export class AssetService {
  apiName = 'Default';

  private restService = inject(RestService);

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, AssetDto>({ method: 'GET', url: `/api/app/asset/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetDto>>({ method: 'GET', url: '/api/app/asset', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: CreateAssetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateAssetDto, AssetDto>({ method: 'POST', url: '/api/app/asset', body: input }, { apiName: this.apiName, ...config });

  update = (id: string, input: UpdateAssetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<UpdateAssetDto, AssetDto>({ method: 'PUT', url: `/api/app/asset/${id}`, body: input }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/asset/${id}` }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, AssetDto>({ method: 'POST', url: `/api/app/asset/${id}/submit` }, { apiName: this.apiName, ...config });

  sell = (id: string, disposalDate: string, amount: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({ method: 'POST', url: `/api/app/asset/${id}/sell`, params: { disposalDate, amount } }, { apiName: this.apiName, ...config });

  scrap = (id: string, disposalDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDto>({ method: 'POST', url: `/api/app/asset/${id}/scrap`, params: { disposalDate } }, { apiName: this.apiName, ...config });

  getCategories = (config?: Partial<Rest.Config>) =>
    this.restService.request<void, AssetCategoryDto[]>({ method: 'GET', url: '/api/app/asset/categories' }, { apiName: this.apiName, ...config });

  createCategory = (input: CreateUpdateAssetCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateUpdateAssetCategoryDto, AssetCategoryDto>({ method: 'POST', url: '/api/app/asset/category', body: input }, { apiName: this.apiName, ...config });
}


