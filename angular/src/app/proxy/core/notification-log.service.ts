import type { GetNotificationLogListDto, NotificationLogDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NotificationLogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationLogDto>({
      method: 'GET',
      url: `/api/app/notification-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getFailedCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/notification-log/failed-count',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetNotificationLogListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NotificationLogDto>>({
      method: 'GET',
      url: '/api/app/notification-log',
      params: { channel: input.channel, status: input.status, documentType: input.documentType, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}