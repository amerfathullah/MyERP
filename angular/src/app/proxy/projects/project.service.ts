import type { CreateProjectDto, CreateProjectTaskDto, GetProjectListDto, ProjectDto, ProjectTaskDto, UpdateProjectDto, UpdateProjectTaskDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProjectService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectDto>({
      method: 'POST',
      url: `/api/app/project/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  cancelTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTaskDto>({
      method: 'POST',
      url: `/api/app/project/cancel-task/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectDto>({
      method: 'POST',
      url: `/api/app/project/${id}/complete`,
    },
    { apiName: this.apiName,...config });
  

  completeTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTaskDto>({
      method: 'POST',
      url: `/api/app/project/complete-task/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectDto>({
      method: 'POST',
      url: '/api/app/project',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createTask = (input: CreateProjectTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTaskDto>({
      method: 'POST',
      url: '/api/app/project/task',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/project/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/project/task/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectDto>({
      method: 'GET',
      url: `/api/app/project/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetProjectListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProjectDto>>({
      method: 'GET',
      url: '/api/app/project',
      params: { status: input.status, filter: input.filter, companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTasks = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTaskDto[]>({
      method: 'GET',
      url: `/api/app/project/tasks/${projectId}`,
    },
    { apiName: this.apiName,...config });
  

  startTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTaskDto>({
      method: 'POST',
      url: `/api/app/project/start-task/${taskId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectDto>({
      method: 'PUT',
      url: `/api/app/project/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateTask = (taskId: string, input: UpdateProjectTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProjectTaskDto>({
      method: 'PUT',
      url: `/api/app/project/task/${taskId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}