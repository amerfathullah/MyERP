import type { CreateUpdatePartyTypeDto, GetPartyTypeListDto, PartyTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartyTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePartyTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyTypeDto>({
      method: 'POST',
      url: '/api/app/party-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/party-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyTypeDto>({
      method: 'GET',
      url: `/api/app/party-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyTypeDto[]>({
      method: 'GET',
      url: '/api/app/party-type/list',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetPartyTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PartyTypeDto>>({
      method: 'GET',
      url: '/api/app/party-type',
      params: { accountType: input.accountType, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePartyTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyTypeDto>({
      method: 'PUT',
      url: `/api/app/party-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}