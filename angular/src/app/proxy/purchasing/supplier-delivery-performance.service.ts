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
      url: '/api/app/supplier-delivery-performance/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}