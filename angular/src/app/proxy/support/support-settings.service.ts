import type { SaveSupportSettingsDto, SupportSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupportSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getForCompany = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportSettingsDto>({
      method: 'GET',
      url: `/api/app/support-settings/for-company/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveSupportSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportSettingsDto>({
      method: 'POST',
      url: '/api/app/support-settings/save',
      body: input,
    },
    { apiName: this.apiName,...config });
}