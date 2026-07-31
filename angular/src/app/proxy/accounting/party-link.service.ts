import type { CreatePartyLinkDto, PartyLinkDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartyLinkService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreatePartyLinkDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyLinkDto>({
      method: 'POST',
      url: '/api/app/party-link',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/party-link/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PartyLinkDto>>({
      method: 'GET',
      url: '/api/app/party-link',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}