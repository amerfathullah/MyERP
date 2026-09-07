import type { CreateUpdateProjectUpdateDto, GetProjectUpdateListDto, ProjectUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProjectUpdateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateProjectUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectUpdateDto>({
      method: 'POST',
      url: '/api/app/project-update',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/project-update/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectUpdateDto>({
      method: 'GET',
      url: `/api/app/project-update/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetProjectUpdateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProjectUpdateDto>>({
      method: 'GET',
      url: '/api/app/project-update',
      params: { filter: input.filter, projectId: input.projectId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateProjectUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectUpdateDto>({
      method: 'PUT',
      url: `/api/app/project-update/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}