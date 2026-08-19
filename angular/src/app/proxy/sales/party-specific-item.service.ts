import type { CreateUpdatePartySpecificItemDto, GetPartySpecificItemListDto, PartySpecificItemDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartySpecificItemService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdatePartySpecificItemDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartySpecificItemDto>({
      method: 'POST',
      url: '/api/app/party-specific-item',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/party-specific-item/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartySpecificItemDto>({
      method: 'GET',
      url: `/api/app/party-specific-item/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetPartySpecificItemListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PartySpecificItemDto>>({
      method: 'GET',
      url: '/api/app/party-specific-item',
      params: { partyType: input.partyType, partyId: input.partyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdatePartySpecificItemDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartySpecificItemDto>({
      method: 'PUT',
      url: `/api/app/party-specific-item/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
