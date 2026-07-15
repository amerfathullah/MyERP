import type { AddTimeLogDto, CreateJobCardDto, GetJobCardListDto, JobCardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class JobCardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addTimeLog = (id: string, input: AddTimeLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      url: `/api/app/job-card/${id}/time-log`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      url: `/api/app/job-card/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      url: `/api/app/job-card/${id}/complete`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateJobCardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      url: '/api/app/job-card',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'GET',
      url: `/api/app/job-card/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetJobCardListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobCardDto>>({
      method: 'GET',
      url: '/api/app/job-card',
      params: { workOrderId: input.workOrderId, companyId: input.companyId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  start = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      url: `/api/app/job-card/${id}/start`,
    },
    { apiName: this.apiName,...config });
}