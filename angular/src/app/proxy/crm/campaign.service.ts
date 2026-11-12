import type { CampaignDto, CreateUpdateCampaignDto, GetCampaignListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CampaignService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  create = (input: CreateUpdateCampaignDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/campaign',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/campaign/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/campaign/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetCampaignListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CampaignDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/campaign',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getUtmCampaign = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/campaign/${id}/utm-campaign`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateUpdateCampaignDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/campaign/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
