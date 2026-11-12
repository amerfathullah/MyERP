import type { GetPartyDetailsInput, PartyDetailsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartyDetailsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getCustomerDetails = (input: GetPartyDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDetailsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/party-details/customer-details',
      params: { partyId: input.partyId, companyId: input.companyId, priceListId: input.priceListId },
    },
      { apiName: this.apiName,...config });
  

  
  getProspectDetails = (input: GetPartyDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDetailsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/party-details/prospect-details',
      params: { partyId: input.partyId, companyId: input.companyId, priceListId: input.priceListId },
    },
      { apiName: this.apiName,...config });
  

  
  getSupplierDetails = (input: GetPartyDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDetailsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/party-details/supplier-details',
      params: { partyId: input.partyId, companyId: input.companyId, priceListId: input.priceListId },
    },
      { apiName: this.apiName,...config });
}
