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

  // --- Maintenance Visit ---

  getVisitList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<any>>({
      method: 'GET',
      url: '/api/app/maintenance/visit-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount,
        completionStatus: input.completionStatus, maintenanceType: input.maintenanceType,
        maintenanceScheduleId: input.maintenanceScheduleId, customerId: input.customerId },
    },
    { apiName: this.apiName,...config });

  getVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'GET',
      url: `/api/app/maintenance/${id}/visit`,
    },
    { apiName: this.apiName,...config });

  createVisit = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'POST',
      url: '/api/app/maintenance/visit',
      body: input,
    },
    { apiName: this.apiName,...config });

  updateVisit = (id: string, input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'PUT',
      url: `/api/app/maintenance/${id}/visit`,
      body: input,
    },
    { apiName: this.apiName,...config });

  completeVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/complete-visit`,
    },
    { apiName: this.apiName,...config });

  cancelVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'POST',
      url: `/api/app/maintenance/${id}/cancel-visit`,
    },
    { apiName: this.apiName,...config });

  deleteVisit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/maintenance/${id}/visit`,
    },
    { apiName: this.apiName,...config });
}