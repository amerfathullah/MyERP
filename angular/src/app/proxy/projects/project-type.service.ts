import type { CreateUpdateProjectTypeDto, ProjectTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProjectTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateProjectTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTypeDto>({
      method: 'POST',
      url: '/api/app/project-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/project-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTypeDto>({
      method: 'GET',
      url: `/api/app/project-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProjectTypeDto>>({
      method: 'GET',
      url: '/api/app/project-type',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateProjectTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTypeDto>({
      method: 'PUT',
      url: `/api/app/project-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}