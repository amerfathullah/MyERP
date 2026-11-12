import type { EmailDigestSendResultDto, EmailDigestSettingsDto, GetEmailDigestSettingsInput, SendEmailDigestNowInput, UpdateEmailDigestSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmailDigestService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getSettings = (input: GetEmailDigestSettingsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailDigestSettingsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/email-digest/settings',
      params: { companyId: input.companyId },
    },
      { apiName: this.apiName,...config });
  

  
  sendNow = (input: SendEmailDigestNowInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailDigestSendResultDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/email-digest/send-now',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  updateSettings = (input: UpdateEmailDigestSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailDigestSettingsDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: '/api/app/email-digest/settings',
      body: input,
    },
      { apiName: this.apiName,...config });
}
