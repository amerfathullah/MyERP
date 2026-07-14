import type { CreateMaintenanceScheduleDto, MaintenanceScheduleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaintenanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createSchedule = (input: CreateMaintenanceScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: '/api/app/maintenance/schedule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getSchedule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'GET',
      url: `/api/app/maintenance/${id}/schedule`,
    },
    { apiName: this.apiName,...config });
  

  getScheduleList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MaintenanceScheduleDto>>({
      method: 'GET',
      url: '/api/app/maintenance/schedule-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submitSchedule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/submit-schedule`,
    },
    { apiName: this.apiName,...config });
}