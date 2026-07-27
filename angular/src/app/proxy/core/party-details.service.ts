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
      method: 'POST',
      url: '/api/app/party-details/customer-details',
      body: input,
    },
    { apiName: this.apiName, ...config });

  getSupplierDetails = (input: GetPartyDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDetailsDto>({
      method: 'POST',
      url: '/api/app/party-details/supplier-details',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
