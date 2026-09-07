import type { CreateUpdateTerritoryDto, GetTerritoryListDto, TerritoryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TerritoryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateTerritoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TerritoryDto>({
      method: 'POST',
      url: '/api/app/territory',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/territory/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TerritoryDto>({
      method: 'GET',
      url: `/api/app/territory/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTerritoryListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TerritoryDto>>({
      method: 'GET',
      url: '/api/app/territory',
      params: { parentId: input.parentId, isGroup: input.isGroup, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateTerritoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TerritoryDto>({
      method: 'PUT',
      url: `/api/app/territory/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}