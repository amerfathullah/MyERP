import type { CreateUpdatePosProfileDto, GetPosProfileListDto, PosProfileDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PosProfileService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePosProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: '/api/app/pos-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/pos-profile/${id}`,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: `/api/app/pos-profile/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: `/api/app/pos-profile/${id}/enable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'GET',
      url: `/api/app/pos-profile/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetPosProfileListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosProfileDto>>({
      method: 'GET',
      url: '/api/app/pos-profile',
      params: { companyId: input.companyId, filter: input.filter, isDisabled: input.isDisabled, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePosProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'PUT',
      url: `/api/app/pos-profile/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}