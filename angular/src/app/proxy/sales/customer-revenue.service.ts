import type { CustomerRevenueReportDto, RegisterFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomerRevenueService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerRevenueReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/customer-revenue/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, customerId: input.customerId, customerGroupId: input.customerGroupId, supplierId: input.supplierId, supplierGroupId: input.supplierGroupId, includePayments: input.includePayments },
    },
      { apiName: this.apiName,...config });
}
