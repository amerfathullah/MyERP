import type { CreateUpdatePromotionalSchemeDto, PromotionalSchemeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface GetPromotionalSchemeListDto {
  companyId?: string;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

@Injectable({
  providedIn: 'root',
})
export class PromotionalSchemeService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdatePromotionalSchemeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PromotionalSchemeDto>({
      method: 'POST',
      url: '/api/app/promotional-scheme',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/promotional-scheme/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PromotionalSchemeDto>({
      method: 'GET',
      url: `/api/app/promotional-scheme/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetPromotionalSchemeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PromotionalSchemeDto>>({
      method: 'GET',
      url: '/api/app/promotional-scheme',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdatePromotionalSchemeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PromotionalSchemeDto>({
      method: 'PUT',
      url: `/api/app/promotional-scheme/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
