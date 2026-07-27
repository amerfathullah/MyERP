import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ErpSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getGroup = (group: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Record<string, string>>({
      method: 'GET',
      url: `/api/app/erp-settings/group`,
      params: { group },
    }, { apiName: this.apiName, ...config });

  update = (settings: Record<string, string>, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/erp-settings/update',
      body: settings,
    }, { apiName: this.apiName, ...config });

  get = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      url: `/api/app/erp-settings`,
      params: { name },
    }, { apiName: this.apiName, ...config });

  set = (name: string, value: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/erp-settings/set',
      params: { name, value },
    }, { apiName: this.apiName, ...config });
}
