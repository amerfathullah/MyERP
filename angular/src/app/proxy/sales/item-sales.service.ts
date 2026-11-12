import type { GetItemWiseSalesHistoryDto, ItemSalesReportDto, ItemWiseSalesHistoryReportDto, RegisterFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemSalesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getHistoryReport = (input: GetItemWiseSalesHistoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemWiseSalesHistoryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/item-sales/history-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, itemId: input.itemId, itemCode: input.itemCode, itemGroupId: input.itemGroupId, customerId: input.customerId },
    },
      { apiName: this.apiName,...config });
  

  
  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemSalesReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/item-sales/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, customerId: input.customerId, customerGroupId: input.customerGroupId, supplierId: input.supplierId, supplierGroupId: input.supplierGroupId, includePayments: input.includePayments },
    },
      { apiName: this.apiName,...config });
}
