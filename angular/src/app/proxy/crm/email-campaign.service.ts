import type { CreateEmailCampaignDto, EmailCampaignDto, GetEmailCampaignListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmailCampaignService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateEmailCampaignDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailCampaignDto>({
      method: 'POST',
      url: '/api/app/email-campaign',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/email-campaign/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailCampaignDto>({
      method: 'GET',
      url: `/api/app/email-campaign/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetEmailCampaignListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EmailCampaignDto>>({
      method: 'GET',
      url: '/api/app/email-campaign',
      params: { campaignId: input.campaignId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  unsubscribe = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailCampaignDto>({
      method: 'POST',
      url: `/api/app/email-campaign/${id}/unsubscribe`,
    },
    { apiName: this.apiName,...config });
}