import type { CreateMaintenanceVisitDto, GetMaintenanceVisitListDto, MaintenanceVisitDto, MaintenanceVisitSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaintenanceVisitService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: `/api/app/maintenance-visit/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateMaintenanceVisitDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: '/api/app/maintenance-visit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/maintenance-visit/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'GET',
      url: `/api/app/maintenance-visit/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetMaintenanceVisitListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MaintenanceVisitDto>>({
      method: 'GET',
      url: '/api/app/maintenance-visit',
      params: { filter: input.filter, customerId: input.customerId, maintenanceScheduleId: input.maintenanceScheduleId, maintenanceType: input.maintenanceType, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getSummary = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitSummaryDto>({
      method: 'GET',
      url: `/api/app/maintenance-visit/${id}/summary`,
    },
    { apiName: this.apiName,...config });
  

  makeFromWarrantyClaim = (warrantyClaimId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreateMaintenanceVisitDto>({
      method: 'POST',
      url: `/api/app/maintenance-visit/make-from-warranty-claim/${warrantyClaimId}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'POST',
      url: `/api/app/maintenance-visit/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateMaintenanceVisitDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaintenanceVisitDto>({
      method: 'PUT',
      url: `/api/app/maintenance-visit/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}