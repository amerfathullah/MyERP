import type { ActivityCostDto, CreateUpdateActivityCostDto, GetActivityCostListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ActivityCostService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateActivityCostDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityCostDto>({
      method: 'POST',
      url: '/api/app/activity-cost',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/activity-cost/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityCostDto>({
      method: 'GET',
      url: `/api/app/activity-cost/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetActivityCostListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ActivityCostDto>>({
      method: 'GET',
      url: '/api/app/activity-cost',
      params: { employeeId: input.employeeId, activityTypeId: input.activityTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateActivityCostDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ActivityCostDto>({
      method: 'PUT',
      url: `/api/app/activity-cost/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
