import type { CreateMaintenanceScheduleDto, GetMaintenanceScheduleListDto, MaintenanceScheduleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaintenanceScheduleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: `/api/app/maintenance-schedule/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateMaintenanceScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: '/api/app/maintenance-schedule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/maintenance-schedule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  generateSchedule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: `/api/app/maintenance-schedule/${id}/generate-schedule`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'GET',
      url: `/api/app/maintenance-schedule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetMaintenanceScheduleListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MaintenanceScheduleDto>>({
      method: 'GET',
      url: '/api/app/maintenance-schedule',
      params: { filter: input.filter, customerId: input.customerId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: `/api/app/maintenance-schedule/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateMaintenanceScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'PUT',
      url: `/api/app/maintenance-schedule/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}