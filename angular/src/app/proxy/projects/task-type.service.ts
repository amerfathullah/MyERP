import type { CreateUpdateTaskTypeDto, GetTaskTypeListDto, TaskTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaskTypeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateTaskTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaskTypeDto>({
      method: 'POST',
      url: '/api/app/task-type',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/task-type/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaskTypeDto>({
      method: 'GET',
      url: `/api/app/task-type/${id}`,
    },
    { apiName: this.apiName, ...config });

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaskTypeDto[]>({
      method: 'GET',
      url: '/api/app/task-type/all-list',
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetTaskTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TaskTypeDto>>({
      method: 'GET',
      url: '/api/app/task-type',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateTaskTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaskTypeDto>({
      method: 'PUT',
      url: `/api/app/task-type/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
