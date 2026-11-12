import type { CreateQiTemplateDto, QiTemplateDto, UpdateQiTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QualityInspectionTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  create = (input: CreateQiTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QiTemplateDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/quality-inspection-template',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quality-inspection-template/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QiTemplateDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/quality-inspection-template/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QiTemplateDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/quality-inspection-template',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  toggle = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/quality-inspection-template/${id}/toggle`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: UpdateQiTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QiTemplateDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/quality-inspection-template/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
