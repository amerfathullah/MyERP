import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';

export interface AddressDto {
  id?: string;
  title?: string;
  addressType?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  phone?: string;
  email?: string;
  partyType?: string;
  partyId?: string;
  isPrimaryAddress?: boolean;
  isShippingAddress?: boolean;
}

@Injectable({ providedIn: 'root' })
export class AddressService {
  apiName = 'Default';
  private restService = inject(RestService);

  getAddressesForParty = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, AddressDto[]>({ method: 'GET', url: '/api/app/address/addresses-for-party', params: { partyType, partyId } }, { apiName: this.apiName, ...config });

  create = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AddressDto>({ method: 'POST', url: '/api/app/address', body: input }, { apiName: this.apiName, ...config });

  update = (id: string, input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AddressDto>({ method: 'PUT', url: `/api/app/address/${id}`, body: input }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/address/${id}` }, { apiName: this.apiName, ...config });
}


