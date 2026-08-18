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
      url: '/api/app/email-digest/settings',
      params: { companyId: input.companyId },
    },
    { apiName: this.apiName,...config });


  updateSettings = (input: UpdateEmailDigestSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailDigestSettingsDto>({
      method: 'PUT',
      url: '/api/app/email-digest/settings',
      body: input,
    },
    { apiName: this.apiName,...config });


  sendNow = (input: SendEmailDigestNowInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailDigestSendResultDto>({
      method: 'POST',
      url: '/api/app/email-digest/send-now',
      body: input,
    },
    { apiName: this.apiName,...config });
}
