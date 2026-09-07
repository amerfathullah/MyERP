import type { AccountsSettingsDto, UpdateAccountsSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountsSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountsSettingsDto>({
      method: 'GET',
      url: '/api/app/accounts-settings',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateAccountsSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountsSettingsDto>({
      method: 'PUT',
      url: '/api/app/accounts-settings',
      body: input,
    },
    { apiName: this.apiName,...config });
}