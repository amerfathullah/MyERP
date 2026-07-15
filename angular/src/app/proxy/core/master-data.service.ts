import type { CostCenterLookupDto, ItemGroupLookupDto, ModeOfPaymentLookupDto, PaymentTermsLookupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MasterDataService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getCostCenters = (companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostCenterLookupDto[]>({
      method: 'GET',
      url: '/api/app/master-data/cost-centers',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getItemGroups = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemGroupLookupDto[]>({
      method: 'GET',
      url: '/api/app/master-data/item-groups',
    },
    { apiName: this.apiName,...config });
  

  getModesOfPayment = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ModeOfPaymentLookupDto[]>({
      method: 'GET',
      url: '/api/app/master-data/modes-of-payment',
    },
    { apiName: this.apiName,...config });
  

  getPaymentTerms = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentTermsLookupDto[]>({
      method: 'GET',
      url: '/api/app/master-data/payment-terms',
    },
    { apiName: this.apiName,...config });
}