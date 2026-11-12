import type { PaymentTermsStatusForSalesOrderReportDto, PaymentTermsStatusForSalesOrderRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentTermsStatusForSalesOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: PaymentTermsStatusForSalesOrderRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentTermsStatusForSalesOrderReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/payment-terms-status-for-sales-order/report',
      params: { companyId: input.companyId, customerId: input.customerId, salesOrderId: input.salesOrderId, fromDate: input.fromDate, toDate: input.toDate, status: input.status, onlyImmediateUpcomingTerm: input.onlyImmediateUpcomingTerm },
    },
      { apiName: this.apiName,...config });
}
