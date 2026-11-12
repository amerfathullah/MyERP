import type { CustomerPerformanceDto, PoFulfillmentReportDto, SupplierPerformanceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartyPerformanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getCustomerPerformance = (customerId: string, companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerPerformanceDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/party-performance/customer-performance',
      params: { customerId, companyId },
    },
      { apiName: this.apiName,...config });
  

  
  getPoFulfillmentReport = (companyId: string, supplierId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PoFulfillmentReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/party-performance/po-fulfillment-report',
      params: { companyId, supplierId },
    },
      { apiName: this.apiName,...config });
  

  
  getSupplierPerformance = (supplierId: string, companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierPerformanceDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/party-performance/supplier-performance',
      params: { supplierId, companyId },
    },
      { apiName: this.apiName,...config });
}
