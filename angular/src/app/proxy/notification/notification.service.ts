import type { AppNotificationDto, NotificationSummaryDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NotificationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppNotificationDto>>({
      method: 'GET',
      url: '/api/app/notification',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getSummary = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationSummaryDto>({
      method: 'GET',
      url: '/api/app/notification/summary',
    },
    { apiName: this.apiName,...config });
  

  markAllAsRead = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/notification/mark-all-as-read',
    },
    { apiName: this.apiName,...config });
  

  markAsRead = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/notification/${id}/mark-as-read`,
    },
    { apiName: this.apiName,...config });
}