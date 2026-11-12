import type { IncomingCallSettingsDto, UpdateIncomingCallSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IncomingCallSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IncomingCallSettingsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/incoming-call-settings',
    },
      { apiName: this.apiName,...config });
  

  
  getActiveEmployeeGroup = (dayOfWeek: any, time: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/incoming-call-settings/active-employee-group',
      params: { dayOfWeek, time },
    },
      { apiName: this.apiName,...config });
  

  
  update = (input: UpdateIncomingCallSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IncomingCallSettingsDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: '/api/app/incoming-call-settings',
      body: input,
    },
      { apiName: this.apiName,...config });
}
