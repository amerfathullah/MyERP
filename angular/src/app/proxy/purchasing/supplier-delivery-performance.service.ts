import type { DeliveryPerformanceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RegisterFilterDto } from '../sales/models';

@Injectable({
  providedIn: 'root',
})
export class SupplierDeliveryPerformanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryPerformanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/supplier-delivery-performance/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, customerId: input.customerId, customerGroupId: input.customerGroupId, supplierId: input.supplierId, supplierGroupId: input.supplierGroupId, includePayments: input.includePayments },
    },
      { apiName: this.apiName,...config });
}
