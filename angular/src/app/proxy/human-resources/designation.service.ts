import type { CreateUpdateDesignationDto, DesignationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DesignationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateDesignationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DesignationDto>({
      method: 'POST',
      url: '/api/app/designation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/designation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DesignationDto>({
      method: 'GET',
      url: `/api/app/designation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DesignationDto>>({
      method: 'GET',
      url: '/api/app/designation',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateDesignationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DesignationDto>({
      method: 'PUT',
      url: `/api/app/designation/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}