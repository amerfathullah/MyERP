import type { CodeListDto, CreateUpdateCodeListDto, GetCodeListListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CodeListService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateCodeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CodeListDto>({
      method: 'POST',
      url: '/api/app/code-list',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/code-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CodeListDto>({
      method: 'GET',
      url: `/api/app/code-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetCodeListListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CodeListDto>>({
      method: 'GET',
      url: '/api/app/code-list',
      params: { filter: input.filter, publisher: input.publisher, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateCodeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CodeListDto>({
      method: 'PUT',
      url: `/api/app/code-list/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}