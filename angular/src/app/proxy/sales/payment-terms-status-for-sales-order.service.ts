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
      method: 'POST',
      url: '/api/app/payment-terms-status-for-sales-order/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
