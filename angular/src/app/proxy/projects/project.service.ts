import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { ProjectDto, CreateProjectDto, UpdateProjectDto, ProjectTaskDto, CreateProjectTaskDto, UpdateProjectTaskDto } from './models';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectDto>({ method: 'GET', url: `/api/app/project/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProjectDto>>({ method: 'GET', url: '/api/app/project', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: CreateProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateProjectDto, ProjectDto>({ method: 'POST', url: '/api/app/project', body: input }, { apiName: this.apiName, ...config });

  update = (id: string, input: UpdateProjectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<UpdateProjectDto, ProjectDto>({ method: 'PUT', url: `/api/app/project/${id}`, body: input }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/project/${id}` }, { apiName: this.apiName, ...config });

  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectDto>({ method: 'POST', url: `/api/app/project/${id}/complete` }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectDto>({ method: 'POST', url: `/api/app/project/${id}/cancel` }, { apiName: this.apiName, ...config });

  getTasks = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectTaskDto[]>({ method: 'GET', url: `/api/app/project/${projectId}/tasks` }, { apiName: this.apiName, ...config });

  createTask = (input: CreateProjectTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateProjectTaskDto, ProjectTaskDto>({ method: 'POST', url: '/api/app/project/task', body: input }, { apiName: this.apiName, ...config });

  updateTask = (taskId: string, input: UpdateProjectTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<UpdateProjectTaskDto, ProjectTaskDto>({ method: 'PUT', url: `/api/app/project/task/${taskId}`, body: input }, { apiName: this.apiName, ...config });

  deleteTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/project/task/${taskId}` }, { apiName: this.apiName, ...config });

  startTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectTaskDto>({ method: 'POST', url: `/api/app/project/task/${taskId}/start` }, { apiName: this.apiName, ...config });

  completeTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectTaskDto>({ method: 'POST', url: `/api/app/project/task/${taskId}/complete` }, { apiName: this.apiName, ...config });

  cancelTask = (taskId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProjectTaskDto>({ method: 'POST', url: `/api/app/project/task/${taskId}/cancel` }, { apiName: this.apiName, ...config });
}
