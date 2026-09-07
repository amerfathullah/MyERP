import type { SupplierQuotationComparisonDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierQuotationComparisonService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getComparisonByIds = (quotationIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationComparisonDto>({
      method: 'GET',
      url: '/api/app/supplier-quotation-comparison/comparison-by-ids',
      params: { quotationIds },
    },
    { apiName: this.apiName,...config });
  

  getComparisonByRfq = (rfqId: string, status?: string, orderStatus?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationComparisonDto>({
      method: 'GET',
      url: `/api/app/supplier-quotation-comparison/comparison-by-rfq/${rfqId}`,
      params: { status, orderStatus },
    },
    { apiName: this.apiName,...config });
}