import type { SubscriptionSettingsDto, UpdateSubscriptionSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubscriptionSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionSettingsDto>({
      method: 'GET',
      url: '/api/app/subscription-settings',
    },
    { apiName: this.apiName, ...config });

  update = (input: UpdateSubscriptionSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionSettingsDto>({
      method: 'PUT',
      url: '/api/app/subscription-settings',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
