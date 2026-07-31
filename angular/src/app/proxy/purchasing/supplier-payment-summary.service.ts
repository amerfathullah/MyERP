import type { SupplierPaymentSummaryReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RegisterFilterDto } from '../sales/models';

@Injectable({
  providedIn: 'root',
})
export class SupplierPaymentSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierPaymentSummaryReportDto>({
      method: 'GET',
      url: '/api/app/supplier-payment-summary/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}