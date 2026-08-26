import type { CrmSettingsDto, UpdateCrmSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CrmSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmSettingsDto>({
      method: 'GET',
      url: '/api/app/crm-settings',
    },
    { apiName: this.apiName, ...config });

  update = (input: UpdateCrmSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmSettingsDto>({
      method: 'PUT',
      url: '/api/app/crm-settings',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
