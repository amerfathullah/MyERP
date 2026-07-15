import type { ActivityCostDto, ActivityTypeDto, CreateActivityTypeDto, SetActivityCostDto, UpdateActivityTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ActivityTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateActivityTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityTypeDto>({
      method: 'POST',
      url: '/api/app/activity-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/activity-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteCost = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/activity-type/${id}/cost`,
    },
    { apiName: this.apiName,...config });
  

  getCostsForActivity = (activityTypeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityCostDto[]>({
      method: 'GET',
      url: `/api/app/activity-type/costs-for-activity/${activityTypeId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityTypeDto[]>({
      method: 'GET',
      url: '/api/app/activity-type',
    },
    { apiName: this.apiName,...config });
  

  setEmployeeCost = (input: SetActivityCostDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityCostDto>({
      method: 'POST',
      url: '/api/app/activity-type/set-employee-cost',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateActivityTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityTypeDto>({
      method: 'PUT',
      url: `/api/app/activity-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}