import type { CreateUpdateProjectTemplateDto, ProjectTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProjectTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  create = (input: CreateUpdateProjectTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTemplateDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/project-template',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/project-template/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTemplateDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/project-template/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ProjectTemplateDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/project-template',
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateUpdateProjectTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTemplateDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/project-template/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
