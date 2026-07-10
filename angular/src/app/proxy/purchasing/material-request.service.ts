import type { CreateMaterialRequestDto, GetMaterialRequestListDto, MaterialRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaterialRequestService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MaterialRequestDto>>({
      method: 'GET',
      url: '/api/app/material-request',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount, sorting: input.sorting },
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'GET',
      url: `/api/app/material-request/${id}`,
    },
    { apiName: this.apiName, ...config });

  create = (input: CreateMaterialRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      url: '/api/app/material-request',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/material-request/${id}`,
    },
    { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      url: `/api/app/material-request/${id}/submit`,
    },
    { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      url: `/api/app/material-request/${id}/cancel`,
    },
    { apiName: this.apiName, ...config });
}
