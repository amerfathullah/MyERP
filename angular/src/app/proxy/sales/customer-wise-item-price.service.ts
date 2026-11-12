import type { CustomerWiseItemPriceReportDto, GetCustomerWiseItemPriceInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomerWiseItemPriceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: GetCustomerWiseItemPriceInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerWiseItemPriceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/customer-wise-item-price/report',
      params: { companyId: input.companyId, customerId: input.customerId, itemId: input.itemId, itemGroupId: input.itemGroupId },
    },
      { apiName: this.apiName,...config });
}
