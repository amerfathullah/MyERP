import type { AddTimeLogDto, CreateJobCardDto, GetJobCardListDto, JobCardCompletionDefaultsDto, JobCardDto, JobCardRawMaterialDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { StockEntryDto } from '../inventory/models';
import type { MaterialRequestDto } from '../purchasing/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class JobCardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  addTimeLog = (id: string, input: AddTimeLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/time-log`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/complete`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateJobCardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/job-card',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createMaterialRequest = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/material-request`,
    },
      { apiName: this.apiName,...config });
  

  
  createMaterialTransfer = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/material-transfer`,
    },
      { apiName: this.apiName,...config });
  

  
  createStockEntryForSemiFgItem = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/stock-entry-for-semi-fg-item`,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/job-card/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getCompletionDefaults = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardCompletionDefaultsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/completion-defaults`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetJobCardListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobCardDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/job-card',
      params: { workOrderId: input.workOrderId, companyId: input.companyId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getRawMaterials = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardRawMaterialDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/raw-materials`,
    },
      { apiName: this.apiName,...config });
  

  
  hold = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/hold`,
    },
      { apiName: this.apiName,...config });
  

  
  resume = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/resume`,
    },
      { apiName: this.apiName,...config });
  

  
  start = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}/start`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateJobCardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/job-card/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
