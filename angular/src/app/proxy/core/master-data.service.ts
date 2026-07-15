import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';

export interface ItemGroupLookupDto {
  id?: string;
  name?: string;
  isGroup?: boolean;
  parentId?: string;
}

export interface ModeOfPaymentLookupDto {
  id?: string;
  name?: string;
  type?: string;
}

export interface CostCenterLookupDto {
  id?: string;
  name?: string;
  isGroup?: boolean;
  parentId?: string;
}

export interface PaymentTermsLookupDto {
  id?: string;
  name?: string;
}

@Injectable({ providedIn: 'root' })
export class MasterDataService {
  apiName = 'Default';
  private restService = inject(RestService);

  getItemGroups = (config?: Partial<Rest.Config>) =>
    this.restService.request<void, ItemGroupLookupDto[]>({ method: 'GET', url: '/api/app/master-data/item-groups' }, { apiName: this.apiName, ...config });

  getModesOfPayment = (config?: Partial<Rest.Config>) =>
    this.restService.request<void, ModeOfPaymentLookupDto[]>({ method: 'GET', url: '/api/app/master-data/modes-of-payment' }, { apiName: this.apiName, ...config });

  getCostCenters = (companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, CostCenterLookupDto[]>({ method: 'GET', url: '/api/app/master-data/cost-centers', params: { companyId } }, { apiName: this.apiName, ...config });

  getPaymentTerms = (config?: Partial<Rest.Config>) =>
    this.restService.request<void, PaymentTermsLookupDto[]>({ method: 'GET', url: '/api/app/master-data/payment-terms' }, { apiName: this.apiName, ...config });
}


