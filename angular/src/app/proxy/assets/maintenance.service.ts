import type { CreateMaintenanceScheduleDto, CreateMaintenanceVisitDto, GetMaintenanceVisitListDto, MaintenanceScheduleDto, MaintenanceVisitDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaintenanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancelVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/cancel-visit`,
    },
    { apiName: this.apiName,...config });
  

  completeVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/complete-visit`,
    },
    { apiName: this.apiName,...config });


  partiallyCompleteVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/partially-complete-visit`,
    },
    { apiName: this.apiName, ...config });


  createSchedule = (input: CreateMaintenanceScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: '/api/app/maintenance/schedule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createVisit = (input: CreateMaintenanceVisitDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: '/api/app/maintenance/visit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/maintenance/${id}/visit`,
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
  

  getVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'GET',
      url: `/api/app/maintenance/${id}/visit`,
    },
    { apiName: this.apiName,...config });
  

  getVisitList = (input: GetMaintenanceVisitListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MaintenanceVisitDto>>({
      method: 'GET',
      url: '/api/app/maintenance/visit-list',
      params: { completionStatus: input.completionStatus, maintenanceScheduleId: input.maintenanceScheduleId, maintenanceType: input.maintenanceType, customerId: input.customerId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submitSchedule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceScheduleDto>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/submit-schedule`,
    },
    { apiName: this.apiName,...config });
  

  updateVisit = (id: string, input: CreateMaintenanceVisitDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'PUT',
      url: `/api/app/maintenance/${id}/visit`,
      body: input,
    },
    { apiName: this.apiName,...config });
}