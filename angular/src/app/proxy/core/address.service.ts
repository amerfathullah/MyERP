import type { AddressDto, CreateUpdateAddressDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AddressService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateAddressDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AddressDto>({
      method: 'POST',
      url: '/api/app/address',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/address/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAddressesForParty = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AddressDto[]>({
      method: 'GET',
      url: `/api/app/address/addresses-for-party/${partyId}`,
      params: { partyType },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAddressDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AddressDto>({
      method: 'PUT',
      url: `/api/app/address/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}