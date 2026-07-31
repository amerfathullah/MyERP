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
      url: '/api/app/party-details/customer-details',
      params: { partyId: input.partyId, companyId: input.companyId },
    },
    { apiName: this.apiName,...config });
  

  getSupplierDetails = (input: GetPartyDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDetailsDto>({
      method: 'GET',
      url: '/api/app/party-details/supplier-details',
      params: { partyId: input.partyId, companyId: input.companyId },
    },
    { apiName: this.apiName,...config });
}