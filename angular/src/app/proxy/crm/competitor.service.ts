import type { CompetitorDto, CreateUpdateCompetitorDto, GetCompetitorListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CompetitorService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateCompetitorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetitorDto>({
      method: 'POST',
      url: '/api/app/competitor',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/competitor/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetitorDto>({
      method: 'GET',
      url: `/api/app/competitor/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetCompetitorListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CompetitorDto>>({
      method: 'GET',
      url: '/api/app/competitor',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateCompetitorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetitorDto>({
      method: 'PUT',
      url: `/api/app/competitor/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}